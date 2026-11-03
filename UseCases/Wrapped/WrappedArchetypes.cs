namespace UseCases.Wrapped;

internal sealed record WrappedArchetypeResult(
    string Code,
    string Emoji,
    string Title,
    string Description,
    string ThemeColor,
    string Gradient,
    double Score,
    bool UsedFallback);

/// <summary>
/// SPEC-014: catalogo de 10 arquetipos (Francotirador #8 y Fanatico #7 diferidos por falta de datos).
/// Gana el mayor score entre los que califican; empate => orden del catalogo.
/// Fallback garantizado: OJO_CLINICO con >= 3 jornadas jugadas, si no CONSERVADOR_SILLON.
/// </summary>
internal static class WrappedArchetypes
{
    internal sealed record Definition(
        string Code,
        string Emoji,
        string Title,
        string Description,
        string ThemeColor,
        string Gradient);

    public static readonly Definition CazadorGigantes = new(
        "CAZADOR_GIGANTES",
        "🦅",
        "El Cazador de Gigantes",
        "Cazaste cuatro o más sorpresas de la temporada. Donde el grupo veía una locura, tú veías una oportunidad.",
        "#16C79A",
        "linear-gradient(165deg, #052E2B 0%, #12A783 55%, #7CF5D4 100%)");

    public static readonly Definition MarquesLocalia = new(
        "MARQUES_LOCALIA",
        "🏰",
        "El Marqués de la Localía",
        "Más del 65% de tus aciertos fueron victorias locales. La cancha pesa y tú lo sabes mejor que nadie.",
        "#FFB020",
        "linear-gradient(165deg, #2A1A00 0%, #B4761A 55%, #FFCB57 100%)");

    public static readonly Definition ConquistadorPatioAjeno = new(
        "CONQUISTADOR_PATIO_AJENO",
        "⚔️",
        "El Conquistador de Patio Ajeno",
        "Ganaste más fuera de casa que muchos en su propia cancha. El patio ajeno no te intimida.",
        "#6C5CE7",
        "linear-gradient(165deg, #150E3D 0%, #4B3AC9 55%, #9C8BFF 100%)");

    public static readonly Definition NotarioEmpate = new(
        "NOTARIO_EMPATE",
        "📑",
        "El Notario del Empate",
        "Apostaste y acertaste más empates que casi todo el grupo. El empate no es aburrido: es tu firma.",
        "#3AA0FF",
        "linear-gradient(165deg, #071B33 0%, #1E5C9A 55%, #6FB6F2 100%)");

    public static readonly Definition ConservadorSillon = new(
        "CONSERVADOR_SILLON",
        "🛡️",
        "El Conservador de Sillón",
        "Alta efectividad con riesgo mínimo: primero el resultado seguro, después la emoción. Un perfil de relojero.",
        "#7FB069",
        "linear-gradient(165deg, #0F1F14 0%, #3C6B3F 55%, #9CCC8A 100%)");

    public static readonly Definition TacticoCaos = new(
        "TACTICO_CAOS",
        "🎢",
        "El Táctico del Caos",
        "Tus semanas van del desastre absoluto a la gloria. Nadie sabe qué versión de ti aparecerá el próximo domingo.",
        "#FF6B6B",
        "linear-gradient(165deg, #2B0711 0%, #B0284A 55%, #FF8FA3 100%)");

    public static readonly Definition AmigoBot = new(
        "AMIGO_BOT",
        "🤖",
        "El Amigo del Bot",
        "Dos o más jornadas las resolvió el autollenado aleatorio y aun así sumaste. El bot también juega contigo.",
        "#00C2FF",
        "linear-gradient(165deg, #04202B 0%, #0B7FA8 55%, #63E3FF 100%)");

    public static readonly Definition OjoClinico = new(
        "OJO_CLINICO",
        "🎯",
        "El Ojo Clínico",
        "Rendimiento regular, sin altibajos pronunciados. El top 3 de consistencia del grupo tiene tu nombre.",
        "#8E9AAF",
        "linear-gradient(165deg, #0D1017 0%, #384254 55%, #A9B5C9 100%)");

    public static readonly Definition Resucitado = new(
        "RESUCITADO",
        "🦺",
        "El Resucitado",
        "Estabas en el último tercio a mitad de temporada y terminaste en el Top 5. Remontada de manual.",
        "#FF8B3D",
        "linear-gradient(165deg, #1C0D02 0%, #A8490E 55%, #FFB37A 100%)");

    public static readonly Definition GuardianHonor = new(
        "GUARDIAN_HONOR",
        "⚽",
        "El Guardián del Honor",
        "Temporada difícil, pero con aciertos emblemáticos en los clásicos. Hay derrotas que se recuerdan con orgullo.",
        "#E0407B",
        "linear-gradient(165deg, #210518 0%, #8C1F4B 55%, #FF85AE 100%)");

    public static readonly IReadOnlyList<Definition> Catalog = new[]
    {
        CazadorGigantes,
        MarquesLocalia,
        ConquistadorPatioAjeno,
        NotarioEmpate,
        ConservadorSillon,
        TacticoCaos,
        AmigoBot,
        OjoClinico,
        Resucitado,
        GuardianHonor
    };

    public static WrappedArchetypeResult Resolve(WrappedSeasonData data)
    {
        var memberId = data.Target.MemberId;
        var picks = data.PicksOf(memberId);
        var hits = picks.Where(p => p.IsHit == true).ToList();

        var hitsCount = hits.Count;
        var effectiveness = picks.Count == 0 ? 0d : (double)hitsCount / picks.Count;
        var upsetHits = hits.Count(p => p.IsUpsetHit);
        var homeHits = data.TargetHomeHits();
        var awayHits = data.TargetAwayHits();
        var homeRatio = hitsCount == 0 ? 0d : (double)homeHits / hitsCount;
        var awayRatio = hitsCount == 0 ? 0d : (double)awayHits / hitsCount;
        var drawHits = data.DrawHits(memberId);
        var weeksPlayed = data.WeeksPlayed(memberId);
        var stdDev = data.WeeklyStdDev(memberId);
        var consistencyRank = data.Members
            .Select(m => new { m.MemberId, StdDev = data.WeeklyStdDev(m.MemberId) })
            .OrderBy(x => x.StdDev)
            .ThenBy(x => x.MemberId)
            .ToList()
            .FindIndex(x => x.MemberId == memberId) + 1;
        var autofilledWeeks = data.AutofilledWeeksWithSuccess(memberId);
        var derbyHits = hits.Count(p => data.MatchOf(p.MatchId).DerbyName is not null);
        var midSeasonRank = data.MidSeasonRank(memberId);
        var position = data.FinalPosition;
        var totalMembers = data.TotalMembers;

        // Notario: empates acertados por encima del p90 del grupo (con grupos chicos, sobre el promedio).
        var notarioQualifies = drawHits >= 2 && (totalMembers >= 5
            ? drawHits > data.P90DrawHits()
            : drawHits > data.AverageDrawHits());

        var bottomThirdLimit = (int)Math.Ceiling(totalMembers * 2d / 3d);

        var candidates = new List<(Definition Definition, bool Qualifies, double Score)>
        {
            (CazadorGigantes, upsetHits >= 4, Math.Min(1d, 0.5 + 0.1 * upsetHits)),
            (MarquesLocalia, hitsCount >= 3 && homeRatio > 0.65, Math.Min(0.95, homeRatio)),
            (ConquistadorPatioAjeno, awayHits >= 3 && awayRatio > 0.5 && awayRatio > homeRatio, Math.Min(0.92, awayRatio)),
            (NotarioEmpate, notarioQualifies, Math.Min(0.9, 0.5 + 0.05 * drawHits)),
            (ConservadorSillon, picks.Count >= 8 && effectiveness >= 0.55 && upsetHits <= 1, Math.Min(0.9, effectiveness)),
            (TacticoCaos, weeksPlayed >= 4 && stdDev >= 2d, Math.Min(0.88, 0.45 + stdDev / 10d)),
            (AmigoBot, autofilledWeeks >= 2, Math.Min(0.86, 0.4 + 0.1 * autofilledWeeks)),
            (OjoClinico, weeksPlayed >= 3 && consistencyRank <= 3,
                Math.Min(0.94, 0.55 + (1d - Math.Min(1d, stdDev / 3d)) * 0.35d)),
            (Resucitado, totalMembers >= 5 && midSeasonRank > bottomThirdLimit && position <= 5, 0.93d),
            (GuardianHonor, position > totalMembers / 2 && derbyHits >= 1 && effectiveness < 0.5, 0.7d)
        };

        var qualifying = candidates.Where(c => c.Qualifies).ToList();

        if (qualifying.Count > 0)
        {
            var best = qualifying[0];
            foreach (var candidate in qualifying.Skip(1))
            {
                if (candidate.Score > best.Score) best = candidate;
            }

            return FromDefinition(best.Definition, best.Score, usedFallback: false);
        }

        var fallback = weeksPlayed >= 3 ? OjoClinico : ConservadorSillon;
        return FromDefinition(fallback, 0d, usedFallback: true);
    }

    private static WrappedArchetypeResult FromDefinition(Definition definition, double score, bool usedFallback) =>
        new(
            definition.Code,
            definition.Emoji,
            definition.Title,
            definition.Description,
            definition.ThemeColor,
            definition.Gradient,
            score,
            usedFallback);
}
