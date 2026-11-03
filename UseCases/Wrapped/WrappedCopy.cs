using System.Globalization;

namespace UseCases.Wrapped;

/// <summary>
/// SPEC-014: copys y paletas deterministas. La variante de texto de cada slide se elige con un hash
/// estable de (SeasonId, TargetMemberId, StoryId); nunca se usa Random ni DateTime.Now.
/// </summary>
internal static class WrappedCopy
{
    private const string DefaultGradient = "linear-gradient(165deg, #0B0F1A 0%, #1B2540 55%, #2F4A7A 100%)";

    private static readonly Dictionary<string, string> StoryGradients = new(StringComparer.Ordinal)
    {
        ["S-01"] = DefaultGradient,
        ["S-03"] = "linear-gradient(165deg, #0A0E1F 0%, #2C1E5A 55%, #6A48D7 100%)",
        ["S-04"] = "linear-gradient(165deg, #1A0508 0%, #5C0F1E 55%, #C22B45 100%)",
        ["S-05"] = "linear-gradient(165deg, #04140C 0%, #10512F 55%, #35C97A 100%)",
        ["S-06"] = "linear-gradient(165deg, #101014 0%, #2E2E38 55%, #6E6E86 100%)",
        ["S-08"] = "linear-gradient(165deg, #0B0A18 0%, #241F52 55%, #5E54C8 100%)",
        ["S-09"] = "linear-gradient(165deg, #1A0716 0%, #56124A 55%, #D655B0 100%)",
        ["S-10"] = "linear-gradient(165deg, #120710 0%, #4A1220 55%, #B23A5B 100%)",
        ["S-11"] = "linear-gradient(165deg, #071018 0%, #123A52 55%, #3FA7C9 100%)",
        ["S-12"] = "linear-gradient(165deg, #1C0A02 0%, #7A2A05 55%, #FF8A2B 100%)",
        ["S-13"] = "linear-gradient(165deg, #0A1418 0%, #1B3D48 55%, #5FA9B8 100%)",
        ["S-14"] = "linear-gradient(165deg, #140C06 0%, #4A2A0E 55%, #C98A3F 100%)",
        ["S-15"] = "linear-gradient(165deg, #1A1503 0%, #5A4708 55%, #E3C34A 100%)"
    };

    public static string GradientFor(string storyId) =>
        StoryGradients.TryGetValue(storyId, out var gradient) ? gradient : DefaultGradient;

    /// <summary>Elige una variante de copy de forma estable para la temporada y el miembro.</summary>
    public static T Choose<T>(int seasonId, int memberId, string storyId, IReadOnlyList<T> options)
    {
        if (options.Count == 0)
            throw new ArgumentException("Se requiere al menos una variante de copy.", nameof(options));

        var hash = StableHash(seasonId, memberId, storyId);
        return options[(int)(hash % (uint)options.Count)];
    }

    /// <summary>FNV-1a de 32 bits: estable entre procesos y plataformas (a diferencia de string.GetHashCode).</summary>
    public static uint StableHash(int seasonId, int memberId, string storyId)
    {
        unchecked
        {
            var hash = 2166136261u;
            hash = Mix(hash, seasonId);
            hash = Mix(hash, memberId);

            foreach (var character in storyId)
            {
                hash ^= character;
                hash *= 16777619u;
            }

            return hash;
        }
    }

    public static string Percent(int numerator, int denominator)
    {
        if (denominator <= 0) return "0%";

        var value = (int)Math.Round(100d * numerator / denominator, MidpointRounding.AwayFromZero);
        return value.ToString(CultureInfo.InvariantCulture) + "%";
    }

    public static string Percent(double ratio)
    {
        var value = (int)Math.Round(100d * ratio, MidpointRounding.AwayFromZero);
        return value.ToString(CultureInfo.InvariantCulture) + "%";
    }

    public static string OneDecimal(double value) =>
        value.ToString("0.0", CultureInfo.InvariantCulture);

    private static uint Mix(uint hash, int value)
    {
        unchecked
        {
            var raw = (uint)value;
            for (var i = 0; i < 4; i++)
            {
                hash ^= (byte)(raw >> (8 * i));
                hash *= 16777619u;
            }

            return hash;
        }
    }
}
