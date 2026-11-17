using Domain.Common;
using Domain.Entities;
using Interface.UseCases.Knockout;

namespace UseCases.Liguilla.Strategies;

/// <summary>
/// Playoffs de la NFL: 14 equipos (7 por conferencia), partido unico y RE-SIEMBRA por conferencia.
/// El seed 1 de cada conferencia descansa en Wild Card y entra directo a Divisional.
/// Convencion de nodos: WC 1-3 AFC y 4-6 NFC, DIV 7-8 AFC y 9-10 NFC, CONF 11 AFC y 12 NFC, SB 13.
/// El seed 1 llega pre-sembrado como TeamA (SeedA = 1) del primer nodo DIV de su conferencia.
/// </summary>
public sealed class NflReseededStrategy : IKnockoutFormatStrategy
{
    public const string FormatCodeValue = KnockoutFormatCodes.NflReseededSingleGame;

    private const string WildCardCode = "WC";
    private const string DivisionalCode = "DIV";
    private const string ConferenceCode = "CONF";
    private const string SuperBowlCode = "SB";

    private const string AfcConference = "afc";
    private const string NfcConference = "nfc";

    private const int WildCardPointsPerHit = 5;
    private const int DivisionalPointsPerHit = 10;
    private const int ConferencePointsPerHit = 20;
    private const int SuperBowlPointsPerHit = 40;
    private const int PerfectBonus = 20;

    private const int WildCardNodeCount = 6;
    private const int DivisionalNodeCount = 4;
    private const int ConferenceNodeCount = 2;
    private const int SuperBowlNodeCount = 1;

    private const int WildCardNodesPerConference = 3;
    private const int DivisionalNodesPerConference = 2;
    private const int TopSeed = 1;

    private static readonly string[] Conferences = { AfcConference, NfcConference };

    private static readonly IReadOnlyList<KnockoutRound> RoundDefinitions = new List<KnockoutRound>
    {
        new(WildCardCode, 1, WildCardNodeCount, WildCardPointsPerHit, WildCardNodeCount),
        new(DivisionalCode, 2, DivisionalNodeCount, DivisionalPointsPerHit, DivisionalNodeCount),
        new(ConferenceCode, 3, ConferenceNodeCount, ConferencePointsPerHit, ConferenceNodeCount),
        new(SuperBowlCode, 4, SuperBowlNodeCount, SuperBowlPointsPerHit, SuperBowlNodeCount)
    };

    /// <summary>Equipo que ocupa un slot de un cruce junto con su siembra.</summary>
    private sealed record Entrant(int TeamId, Team? Team, int? Seed);

    /// <inheritdoc />
    public string FormatCode => FormatCodeValue;

    /// <inheritdoc />
    public IReadOnlyList<KnockoutRound> Rounds => RoundDefinitions;

    /// <inheritdoc />
    public int PerfectBonusPoints => PerfectBonus;

    /// <inheritdoc />
    public string FirstPickableRoundCode => WildCardCode;

    /// <inheritdoc />
    public bool IsFieldReady(IReadOnlyList<LiguillaNode> nodes, bool regularSeasonFinished)
    {
        if (!regularSeasonFinished)
        {
            return false;
        }

        var wildCard = NodesOfRound(nodes, WildCardCode);

        return wildCard.Count == WildCardNodeCount
               && wildCard.All(node => node.TeamAId is not null && node.TeamBId is not null);
    }

    /// <inheritdoc />
    public IReadOnlyList<int> EligibleTeamIds(
        string roundCode,
        IReadOnlyList<LiguillaNode> nodes,
        IReadOnlyDictionary<string, IReadOnlyList<int>> previousPicks)
    {
        var roundIndex = IndexOfRound(roundCode);
        if (roundIndex < 0)
        {
            return Array.Empty<int>();
        }

        if (roundIndex == 0)
        {
            return FieldTeamIds(nodes, roundCode).ToList();
        }

        var eligible = PicksOfRound(previousPicks, RoundDefinitions[roundIndex - 1].Code).Distinct().ToList();
        if (roundCode == DivisionalCode)
        {
            eligible.AddRange(ByeTeamIds(nodes).Where(teamId => !eligible.Contains(teamId)));
        }

        return eligible;
    }

    /// <inheritdoc />
    public void ValidatePicks(
        IReadOnlyList<LiguillaNode> nodes,
        IReadOnlyDictionary<string, IReadOnlyList<int>> picksByRound)
    {
        for (var roundIndex = 0; roundIndex < RoundDefinitions.Count; roundIndex++)
        {
            var round = RoundDefinitions[roundIndex];
            var picks = PicksOfRound(picksByRound, round.Code);

            if (picks.Count != round.WinnersToPick || picks.Distinct().Count() != picks.Count)
            {
                throw new ArgumentException($"Debes elegir {round.WinnersToPick} ganadores distintos en {round.Code}.");
            }

            HashSet<int> allowedTeams;
            if (roundIndex == 0)
            {
                allowedTeams = FieldTeamIds(nodes, round.Code).ToHashSet();
            }
            else
            {
                allowedTeams = PicksOfRound(picksByRound, RoundDefinitions[roundIndex - 1].Code).ToHashSet();
                if (round.Code == DivisionalCode)
                {
                    allowedTeams.UnionWith(ByeTeamIds(nodes));
                }
            }

            if (picks.All(allowedTeams.Contains))
            {
                continue;
            }

            var isLastRound = roundIndex == RoundDefinitions.Count - 1;
            throw isLastRound
                ? new ArgumentException("El Campeón debe ser uno de los finalistas elegidos.")
                : new ArgumentException($"Los equipos de {round.Code} deben haber sido elegidos en la ronda anterior.");
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<LiguillaNode> AdvanceWinners(IReadOnlyList<LiguillaNode> allNodes)
    {
        var modified = new List<LiguillaNode>();

        foreach (var conference in Conferences)
        {
            ReseedDivisional(allNodes, conference, modified);
            SeedConferenceFinal(allNodes, conference, modified);
        }

        SeedSuperBowl(allNodes, modified);

        return modified;
    }

    /// <inheritdoc />
    public NodeOutcome ResolveFromEspn(LiguillaNode node, EspnKnockoutTie tie)
    {
        var winnerEspnId = tie.WinnerEspnTeamId;

        if (!string.IsNullOrWhiteSpace(winnerEspnId))
        {
            if (string.Equals(node.TeamA?.EspnTeamId, winnerEspnId, StringComparison.Ordinal))
            {
                return new NodeOutcome(node.NodeNumber, true, node.TeamAId);
            }

            if (string.Equals(node.TeamB?.EspnTeamId, winnerEspnId, StringComparison.Ordinal))
            {
                return new NodeOutcome(node.NodeNumber, true, node.TeamBId);
            }
        }

        return new NodeOutcome(node.NodeNumber, false, null);
    }

    /// <summary>
    /// Tras Wild Card: el seed 1 (bye) juega contra el PEOR sembrado sobreviviente y los otros dos se enfrentan
    /// entre si (mejor vs intermedio).
    /// </summary>
    private static void ReseedDivisional(IReadOnlyList<LiguillaNode> allNodes, string conference, List<LiguillaNode> modified)
    {
        var wildCard = NodesOfConference(allNodes, WildCardCode, conference);
        var divisional = NodesOfConference(allNodes, DivisionalCode, conference);

        if (wildCard.Count != WildCardNodesPerConference || divisional.Count != DivisionalNodesPerConference)
        {
            return;
        }

        var survivors = wildCard.Select(WinnerOf).ToList();
        if (survivors.Any(survivor => survivor is null))
        {
            return;
        }

        var bye = divisional.SelectMany(node => new[] { SlotA(node), SlotB(node) })
            .FirstOrDefault(entrant => entrant is not null && entrant.Seed == TopSeed);
        if (bye is null)
        {
            return;
        }

        var ordered = survivors
            .OfType<Entrant>()
            .OrderBy(entrant => entrant.Seed ?? int.MaxValue)
            .ToList();

        Assign(divisional[0], bye, ordered[2], modified);
        Assign(divisional[1], ordered[0], ordered[1], modified);
    }

    /// <summary>Tras Divisional: el ganador del primer nodo DIV de la conferencia es TeamA de la final de conferencia.</summary>
    private static void SeedConferenceFinal(IReadOnlyList<LiguillaNode> allNodes, string conference, List<LiguillaNode> modified)
    {
        var divisional = NodesOfConference(allNodes, DivisionalCode, conference);
        var conferenceFinal = NodesOfConference(allNodes, ConferenceCode, conference).FirstOrDefault();

        if (conferenceFinal is null || divisional.Count != DivisionalNodesPerConference)
        {
            return;
        }

        var first = WinnerOf(divisional[0]);
        var second = WinnerOf(divisional[1]);
        if (first is null || second is null)
        {
            return;
        }

        Assign(conferenceFinal, first, second, modified);
    }

    /// <summary>Tras las finales de conferencia: campeon AFC (TeamA) vs campeon NFC (TeamB).</summary>
    private static void SeedSuperBowl(IReadOnlyList<LiguillaNode> allNodes, List<LiguillaNode> modified)
    {
        var superBowl = NodesOfRound(allNodes, SuperBowlCode).FirstOrDefault();
        var afcChampion = NodesOfConference(allNodes, ConferenceCode, AfcConference).FirstOrDefault();
        var nfcChampion = NodesOfConference(allNodes, ConferenceCode, NfcConference).FirstOrDefault();

        if (superBowl is null || afcChampion is null || nfcChampion is null)
        {
            return;
        }

        var afc = WinnerOf(afcChampion);
        var nfc = WinnerOf(nfcChampion);
        if (afc is null || nfc is null)
        {
            return;
        }

        Assign(superBowl, afc, nfc, modified);
    }

    /// <summary>Siembra ambos slots del nodo destino salvo que ya este resuelto; lo registra si cambio.</summary>
    private static void Assign(LiguillaNode target, Entrant slotA, Entrant slotB, List<LiguillaNode> modified)
    {
        if (target.IsResolved)
        {
            return;
        }

        var changed = false;

        if (target.TeamAId != slotA.TeamId || target.SeedA != slotA.Seed)
        {
            target.TeamAId = slotA.TeamId;
            target.TeamA = slotA.Team;
            target.SeedA = slotA.Seed;
            changed = true;
        }

        if (target.TeamBId != slotB.TeamId || target.SeedB != slotB.Seed)
        {
            target.TeamBId = slotB.TeamId;
            target.TeamB = slotB.Team;
            target.SeedB = slotB.Seed;
            changed = true;
        }

        if (changed && !modified.Contains(target))
        {
            modified.Add(target);
        }
    }

    private static Entrant? WinnerOf(LiguillaNode node)
    {
        if (!node.IsResolved || node.WinnerTeamId is null)
        {
            return null;
        }

        if (node.TeamAId == node.WinnerTeamId)
        {
            return SlotA(node);
        }

        return node.TeamBId == node.WinnerTeamId ? SlotB(node) : null;
    }

    private static Entrant? SlotA(LiguillaNode node)
    {
        return node.TeamAId is { } teamId ? new Entrant(teamId, node.TeamA, node.SeedA) : null;
    }

    private static Entrant? SlotB(LiguillaNode node)
    {
        return node.TeamBId is { } teamId ? new Entrant(teamId, node.TeamB, node.SeedB) : null;
    }

    /// <summary>Equipos con descanso: seed 1 presente en los nodos Divisional.</summary>
    private static IEnumerable<int> ByeTeamIds(IReadOnlyList<LiguillaNode> nodes)
    {
        foreach (var node in NodesOfRound(nodes, DivisionalCode))
        {
            if (node.TeamAId is { } teamA && node.SeedA == TopSeed)
            {
                yield return teamA;
            }

            if (node.TeamBId is { } teamB && node.SeedB == TopSeed)
            {
                yield return teamB;
            }
        }
    }

    private static IEnumerable<int> FieldTeamIds(IReadOnlyList<LiguillaNode> nodes, string roundCode)
    {
        return NodesOfRound(nodes, roundCode)
            .SelectMany(node => new[] { node.TeamAId, node.TeamBId })
            .OfType<int>()
            .Distinct();
    }

    private static List<LiguillaNode> NodesOfConference(IReadOnlyList<LiguillaNode> nodes, string roundCode, string conference)
    {
        return NodesOfRound(nodes, roundCode)
            .Where(node => string.Equals(node.Conference, conference, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static List<LiguillaNode> NodesOfRound(IReadOnlyList<LiguillaNode> nodes, string roundCode)
    {
        return nodes
            .Where(node => node.Round == roundCode)
            .OrderBy(node => node.NodeNumber)
            .ToList();
    }

    private static int IndexOfRound(string roundCode)
    {
        for (var index = 0; index < RoundDefinitions.Count; index++)
        {
            if (RoundDefinitions[index].Code == roundCode)
            {
                return index;
            }
        }

        return -1;
    }

    private static IReadOnlyList<int> PicksOfRound(
        IReadOnlyDictionary<string, IReadOnlyList<int>> picksByRound,
        string roundCode)
    {
        return picksByRound.TryGetValue(roundCode, out var picks) && picks is not null
            ? picks
            : Array.Empty<int>();
    }
}
