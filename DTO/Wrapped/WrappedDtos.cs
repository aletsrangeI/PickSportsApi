namespace DTO.Wrapped;

/// <summary>SPEC-014: payload completo del Wrapped de un miembro (se cachea como JSON).</summary>
public class SeasonWrappedDto
{
    public int SeasonId { get; set; }
    public string SeasonName { get; set; } = null!;
    public int FinalPosition { get; set; }
    public int TotalMembers { get; set; }
    public string ArchetypeCode { get; set; } = null!;
    public string ArchetypeEmoji { get; set; } = null!;
    public string ArchetypeTitle { get; set; } = null!;
    public string ArchetypeDescription { get; set; } = null!;
    /// <summary>Color hex (#RRGGBB) derivado del arquetipo.</summary>
    public string ThemeColor { get; set; } = null!;

    /// <summary>Entre 6 y 7 slides ordenadas: WARMUP, ARCHETYPE y 4-5 historias singulares.</summary>
    public List<WrappedSlideDto> Slides { get; set; } = new();
    public WrappedFinalCardDto FinalCard { get; set; } = null!;
}

public class WrappedSlideDto
{
    /// <summary>Id del catalogo del spec: "S-01".."S-15".</summary>
    public string StoryId { get; set; } = null!;
    /// <summary>WARMUP, ARCHETYPE, LONE_WOLF, NIGHTMARE, TALISMAN, CRYPTONITE, CLOCK_HABIT, SOULMATE, NEMESIS,
    /// BIG_HUNT, HOT_STREAK, DRAW_NOTARY, DERBY_KING, TROPHY_CASE.</summary>
    public string SlideType { get; set; } = null!;
    /// <summary>0 = metrica principal; 1..n = nivel de la cascada de fallback usado.</summary>
    public int FallbackLevel { get; set; }
    public string Headline { get; set; } = null!;
    public string Subtitle { get; set; } = null!;
    public string? MatchTitle { get; set; }
    public string? ScoreDisplay { get; set; }
    public string BodyText { get; set; } = null!;
    public string? HighlightMetric { get; set; }
    public string? MentionedUserName { get; set; }
    public string? MentionedUserAvatarUrl { get; set; }
    /// <summary>CSS linear-gradient(...) completo.</summary>
    public string BackgroundGradient { get; set; } = null!;
}

public class WrappedFinalCardDto
{
    public string DisplayName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public string SeasonName { get; set; } = null!;
    public int FinalPosition { get; set; }
    public int TotalMembers { get; set; }
    public string ArchetypeEmoji { get; set; } = null!;
    public string ArchetypeTitle { get; set; } = null!;
    public string ThemeColor { get; set; } = null!;
    /// <summary>Exactamente 4 estadisticas clave.</summary>
    public List<WrappedCardStatDto> Stats { get; set; } = new();
}

public class WrappedCardStatDto
{
    public string Label { get; set; } = null!;
    public string Value { get; set; } = null!;
}

/// <summary>Respuesta de GET /api/quinielas/{id}/wrapped/availability (alimenta el banner del Dashboard).</summary>
public class WrappedAvailabilityDto
{
    public bool IsAvailable { get; set; }
    public int? SeasonId { get; set; }
    public string? SeasonName { get; set; }
}

public class WrappedGenerationResultDto
{
    public int SeasonId { get; set; }
    public int Generated { get; set; }
    public int Skipped { get; set; }
}

public class SetSeasonFinishedDto
{
    public bool IsFinished { get; set; }
}
