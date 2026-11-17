using Domain.Common;
using Domain.Entities;
using Interface.Persistence;
using Interface.UseCases.Knockout;
using Microsoft.Extensions.Logging;
using UseCases.Scoring;

namespace UseCases.Liguilla.Simulation;

/// <summary>
/// Simulacion de QA de cualquier formato de eliminatoria: toma la tabla de ESPN de HOY, arma la primera ronda con el builder del formato
/// y reemplaza los nodos de la temporada de la quiniela (rondas siguientes vacias, kickoffs futuros).
/// </summary>
public class KnockoutSimulationService : IKnockoutSimulationService
{
    private const string EspnClientName = "EspnClient";
    private const string EspnBaseUrl = "https://site.web.api.espn.com/";
    private const string PrimaryHost = "site.web.api.espn.com";
    private const string SecondaryHost = "site.api.espn.com";
    private const int ForbiddenStatusCode = 403;

    private const int FirstRoundLeadDays = 7;
    private const int DaysBetweenRounds = 7;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IKnockoutStrategyResolver _resolver;
    private readonly IReadOnlyList<IKnockoutSimulationBuilder> _builders;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<KnockoutSimulationService> _logger;

    public KnockoutSimulationService(
        IUnitOfWork unitOfWork,
        IKnockoutStrategyResolver resolver,
        IEnumerable<IKnockoutSimulationBuilder> builders,
        IHttpClientFactory httpClientFactory,
        ILogger<KnockoutSimulationService> logger)
    {
        _unitOfWork = unitOfWork;
        _resolver = resolver;
        _builders = builders.ToList();
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<int> SeedAsync(int quinielaId, CancellationToken ct = default)
    {
        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId)
                       ?? throw new KeyNotFoundException($"No se encontró la quiniela con Id {quinielaId}.");
        if (!quiniela.LeagueId.HasValue)
            throw new InvalidOperationException("La quiniela no tiene una liga asignada.");

        var league = quiniela.League ?? await _unitOfWork.Leagues.GetAsync(quiniela.LeagueId.Value)
                     ?? throw new InvalidOperationException("No se encontró la liga de la quiniela.");

        var strategy = _resolver.For(league)
                       ?? throw new InvalidOperationException("La liga de la quiniela no tiene un formato de eliminatoria configurado.");
        var builder = _builders.FirstOrDefault(b => b.FormatCode == strategy.FormatCode)
                      ?? throw new InvalidOperationException($"El formato {strategy.FormatCode} no tiene simulación disponible.");

        var seasonId = await QuinielaSeasonResolver.ResolveSeasonIdAsync(_unitOfWork, quiniela)
                       ?? throw new InvalidOperationException("No se encontró una temporada activa para esta liga.");

        var existingNodes = await _unitOfWork.Liguilla.GetNodesBySeasonAsync(seasonId, ct);
        if (existingNodes.Any(n => n.IsResolved || n.EspnEventGroupKey != null))
            throw new InvalidOperationException("La temporada ya tiene cruces reales de ESPN o resultados; la simulación solo aplica a campos vacíos o simulados.");
        if (await _unitOfWork.Liguilla.AnyBracketsForSeasonAsync(seasonId, ct))
            throw new InvalidOperationException("Ya hay pronósticos enviados en esta temporada; la simulación los dejaría inconsistentes.");

        var json = await FetchStandingsAsync(builder.StandingsPath(league.EspnApiPath ?? string.Empty), ct);
        var ties = builder.Build(json);
        if (ties.Count == 0)
            throw new InvalidOperationException("ESPN no devolvió equipos suficientes para simular el campo.");

        var teams = (await _unitOfWork.Teams.GetByLeagueIdAsync(league.Id))
            .Where(t => !string.IsNullOrEmpty(t.EspnTeamId))
            .GroupBy(t => t.EspnTeamId)
            .ToDictionary(g => g.Key, g => g.First());

        var now = DateTime.UtcNow;
        var seeded = 0;
        var nodeNumber = 1;
        foreach (var round in strategy.Rounds.OrderBy(r => r.Order))
        {
            var kickoff = now.AddDays(FirstRoundLeadDays + DaysBetweenRounds * (round.Order - 1));
            for (var offset = 0; offset < round.NodeCount; offset++, nodeNumber++)
            {
                var tie = ties.FirstOrDefault(t => t.NodeNumber == nodeNumber && t.RoundCode == round.Code);
                var node = existingNodes.FirstOrDefault(n => n.NodeNumber == nodeNumber);
                var isNew = node == null;
                node ??= new LiguillaNode { SeasonId = seasonId, NodeNumber = nodeNumber };

                ResetNode(node, round.Code, ConferenceOfBlock(strategy.FormatCode, round, offset), kickoff);
                if (tie != null)
                    ApplyTie(node, tie, teams);

                if (isNew)
                    await _unitOfWork.Liguilla.AddNodeAsync(node, ct);
                else
                    await _unitOfWork.Liguilla.UpdateNodeAsync(node, ct);
                seeded++;
            }
        }

        await _unitOfWork.Save(ct);
        return seeded;
    }

    private static void ResetNode(LiguillaNode node, string roundCode, string? conference, DateTime kickoff)
    {
        node.Round = roundCode;
        node.Conference = conference;
        node.TeamAId = null;
        node.TeamA = null;
        node.TeamBId = null;
        node.TeamB = null;
        node.SeedA = null;
        node.SeedB = null;
        node.WinnerTeamId = null;
        node.WinnerTeam = null;
        node.IsResolved = false;
        node.ResolvedAtUtc = null;
        node.EspnEventGroupKey = null;
        node.KickoffUtc = kickoff;
        node.Active = true;
    }

    private void ApplyTie(LiguillaNode node, SimulatedTie tie, IReadOnlyDictionary<string, Team> teams)
    {
        if (!teams.TryGetValue(tie.EspnTeamIdA, out var teamA))
        {
            _logger.LogWarning("[KnockoutSimulation] Cruce {Node} omitido: el equipo ESPN {EspnId} no existe en la liga.", tie.NodeNumber, tie.EspnTeamIdA);
            return;
        }

        Team? teamB = null;
        if (tie.EspnTeamIdB != null && !teams.TryGetValue(tie.EspnTeamIdB, out teamB))
        {
            _logger.LogWarning("[KnockoutSimulation] Cruce {Node} omitido: el equipo ESPN {EspnId} no existe en la liga.", tie.NodeNumber, tie.EspnTeamIdB);
            return;
        }

        // Solo las llaves foraneas: los equipos vienen desconectados (AsNoTracking) y asignarlos como navegacion haria que EF los inserte de nuevo.
        node.TeamAId = teamA.Id;
        node.SeedA = tie.SeedA;
        if (teamB == null) return;

        node.TeamBId = teamB.Id;
        node.SeedB = tie.SeedB;
    }

    private async Task<string> FetchStandingsAsync(string path, CancellationToken ct)
    {
        var url = EspnBaseUrl + path.TrimStart('/');
        var client = _httpClientFactory.CreateClient(EspnClientName);

        try
        {
            using var response = await client.GetAsync(url, ct);
            if ((int)response.StatusCode == ForbiddenStatusCode)
            {
                using var fallback = await client.GetAsync(url.Replace(PrimaryHost, SecondaryHost), ct);
                return await ReadBodyAsync(fallback, url, ct);
            }

            return await ReadBodyAsync(response, url, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("No se pudo consultar la tabla de posiciones de ESPN.", ex);
        }
    }

    private async Task<string> ReadBodyAsync(HttpResponseMessage response, string url, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return await response.Content.ReadAsStringAsync(ct);

        _logger.LogWarning("[KnockoutSimulation] ESPN respondió {Status} para {Url}.", (int)response.StatusCode, url);
        throw new InvalidOperationException("ESPN no devolvió la tabla de posiciones.");
    }

    /// <summary>Coherente con EspnKnockoutSyncService: en rondas de varios nodos la primera mitad es "east"/"afc" y la segunda "west"/"nfc".</summary>
    private static string? ConferenceOfBlock(string formatCode, KnockoutRound round, int offset)
    {
        if (round.NodeCount < 2) return null;

        var isFirstHalf = offset < round.NodeCount / 2;
        return formatCode switch
        {
            KnockoutFormatCodes.NhlBestOf7 => isFirstHalf ? "east" : "west",
            KnockoutFormatCodes.NflReseededSingleGame => isFirstHalf ? "afc" : "nfc",
            _ => null
        };
    }
}
