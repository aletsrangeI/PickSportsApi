namespace Interface.UseCases.Knockout;

/// <summary>
/// Un cruce de eliminatoria tal como lo reporta ESPN, normalizado para todas las ligas.
/// Los ids de equipo son los de ESPN (<c>Team.EspnTeamId</c>), no los de la base de datos.
/// </summary>
public record EspnKnockoutTie
{
    /// <summary>Identificador estable del cruce (se guarda en <c>LiguillaNode.EspnEventGroupKey</c>).</summary>
    public string GroupKey { get; init; } = string.Empty;
    /// <summary>Slug de fase de ESPN: "round-of-16", "post-season", "apertura---finals"...</summary>
    public string RoundSlug { get; init; } = string.Empty;
    /// <summary>Titular de ESPN: "East 1st Round - Game 2", "AFC Wild Card Playoffs", "2nd Leg - X advance 4-3 on aggregate".</summary>
    public string Headline { get; init; } = string.Empty;
    public int? LegNumber { get; init; }
    public int TotalLegs { get; init; } = 1;
    public bool SeriesCompleted { get; init; }
    public string? WinnerEspnTeamId { get; init; }
    /// <summary>Victorias por equipo (series al mejor de N, NHL).</summary>
    public IReadOnlyDictionary<string, int> Wins { get; init; } = new Dictionary<string, int>();
    /// <summary>Marcador global por equipo (ida y vuelta, Champions).</summary>
    public IReadOnlyDictionary<string, double> AggregateScore { get; init; } = new Dictionary<string, double>();
    public string? HomeEspnTeamId { get; init; }
    public string? AwayEspnTeamId { get; init; }
    public DateTime? KickoffUtc { get; init; }
    /// <summary>"east"/"west" (NHL), "afc"/"nfc" (NFL); null si la liga no divide por conferencia.</summary>
    public string? Conference { get; init; }
}
