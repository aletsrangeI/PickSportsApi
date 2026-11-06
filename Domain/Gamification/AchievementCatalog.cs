namespace Domain.Gamification;

public record AchievementDefinition(
    string Code,
    string Name,
    string Description,
    string Tier,          // BRONZE | SILVER | GOLD | PLATINUM
    int Gamerscore,
    string? TitleName);   // titulo equipable que otorga (solo Oro/Platino)

/// <summary>SPEC-013: catalogo de trofeos (definido en codigo) y niveles de Cazador.</summary>
public static class AchievementCatalog
{
    public const string Bronze = "BRONZE";
    public const string Silver = "SILVER";
    public const string Gold = "GOLD";
    public const string Platinum = "PLATINUM";

    public const string PrimeraSangre = "PRIMERA_SANGRE";
    public const string OlfatoDeSorpresa = "OLFATO_DE_SORPRESA";
    public const string JornadaCompleta = "JORNADA_COMPLETA";
    public const string FielAlColiseo = "FIEL_AL_COLISEO";
    public const string RachaDeFuego = "RACHA_DE_FUEGO";
    public const string CazadorDeSorpresas = "CAZADOR_DE_SORPRESAS";
    public const string EnElPodio = "EN_EL_PODIO";
    public const string ConstanciaDeAcero = "CONSTANCIA_DE_ACERO";
    public const string SoloYoLoVi = "SOLO_YO_LO_VI";
    public const string SemanaInvicta = "SEMANA_INVICTA";
    public const string TricampeonDeJornada = "TRICAMPEON_DE_JORNADA";
    public const string ColeccionistaDeBronce = "COLECCIONISTA_DE_BRONCE";
    public const string IndiscutidoDelColiseo = "INDISCUTIDO_DEL_COLISEO";

    public static readonly IReadOnlyList<AchievementDefinition> All = new List<AchievementDefinition>
    {
        new(PrimeraSangre, "Primera Sangre", "Tu primer acierto de la temporada.", Bronze, 15, null),
        new(OlfatoDeSorpresa, "Olfato de Sorpresa", "Tu primer acierto en un partido sorpresa.", Bronze, 15, null),
        new(JornadaCompleta, "Jornada Completa", "Pronostica a mano todos los partidos de una jornada.", Bronze, 15, null),
        new(FielAlColiseo, "Fiel al Coliseo", "Juega 5 jornadas calificadas en la temporada.", Bronze, 15, null),
        new(RachaDeFuego, "Racha de Fuego", "Acierta 5 partidos seguidos dentro de una jornada.", Silver, 30, null),
        new(CazadorDeSorpresas, "Cazador de Sorpresas", "Acumula 5 aciertos en partidos sorpresa.", Silver, 30, null),
        new(EnElPodio, "En el Podio", "Termina una jornada en el Top 3.", Silver, 30, null),
        new(ConstanciaDeAcero, "Constancia de Acero", "Juega 10 jornadas sin autollenado.", Silver, 30, null),
        new(SoloYoLoVi, "Solo Yo Lo Vi", "Sé el único que acertó un partido (con 4 o más miembros).", Gold, 75, "Ojo de Halcón"),
        new(SemanaInvicta, "Semana Invicta", "Acierta todos los partidos de una jornada (mínimo 5).", Gold, 75, "Intocable"),
        new(TricampeonDeJornada, "Tricampeón de Jornada", "Sé MVP de la jornada 3 veces en la temporada.", Gold, 75, "Tricampeón"),
        new(ColeccionistaDeBronce, "Coleccionista de Bronce", "Reúne todos los trofeos de Bronce de la temporada.", Platinum, 150, "Coleccionista"),
        new(IndiscutidoDelColiseo, "Indiscutido del Coliseo", "Corónate Campeón de Duelos.", Platinum, 200, "Gladiador Legendario"),
    };

    public static AchievementDefinition? Find(string code) =>
        All.FirstOrDefault(a => string.Equals(a.Code, code, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<AchievementDefinition> BronzeAchievements =>
        All.Where(a => a.Tier == Bronze).ToList();

    private static readonly (int MinGamerscore, string Name)[] Levels =
    {
        (0, "Novato"), (50, "Aprendiz"), (150, "Cazador"),
        (300, "Cazador Experto"), (600, "Maestro Cazador"), (1000, "Leyenda"),
    };

    public static string LevelFor(int gamerscore) =>
        Levels.Last(l => gamerscore >= l.MinGamerscore).Name;

    /// <summary>Gamerscore que falta para el siguiente nivel, o null si ya es el maximo.</summary>
    public static int? NextLevelAt(int gamerscore) =>
        Levels.Where(l => l.MinGamerscore > gamerscore).Select(l => (int?)l.MinGamerscore).FirstOrDefault();
}
