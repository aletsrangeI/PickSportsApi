using DTO.Wrapped;

namespace UseCases.Wrapped;

/// <summary>
/// SPEC-014: motor puro de arquetipos, huella digital y cascadas. A1 reemplaza el cuerpo; la FIRMA es contrato.
/// </summary>
public class SeasonWrappedEngine
{
    /// <summary>Construye el Wrapped del miembro <see cref="WrappedEngineInput.TargetMemberId"/>. Determinista (sin Random ni DateTime.Now).</summary>
    public SeasonWrappedDto Build(WrappedEngineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var data = new WrappedSeasonData(input);
        var archetype = WrappedArchetypes.Resolve(data);

        var slides = new List<WrappedSlideDto>
        {
            WrappedStoryFactory.BuildWarmup(data),
            WrappedStoryFactory.BuildArchetype(data, archetype)
        };

        // S-01 y S-02 son fijas; el resto se ordena por singularidad (empate => StoryId ascendente).
        var eligible = new List<WrappedStory>();
        foreach (var storyId in WrappedStoryFactory.StoryCatalog)
        {
            var story = WrappedStoryFactory.Build(storyId, data);
            if (story is not null) eligible.Add(story);
        }

        var ordered = eligible
            .OrderByDescending(s => Math.Round(s.Singularity, 3))
            .ThenBy(s => s.StoryId, StringComparer.Ordinal)
            .ToList();

        // 7 slides si hay >= 5 historias con singularidad real; si no, 6. S-03/S-04/S-12/S-15
        // garantizan al menos 4 historias elegibles.
        var take = ordered.Count(s => s.Singularity > 0d) >= 5 ? 5 : 4;
        var selected = ordered
            .Take(Math.Min(take, ordered.Count))
            .OrderBy(s => s.StoryId, StringComparer.Ordinal)
            .Select(s => s.Slide)
            .ToList();

        slides.AddRange(selected);

        return new SeasonWrappedDto
        {
            SeasonId = input.SeasonId,
            SeasonName = input.SeasonName,
            FinalPosition = data.FinalPosition,
            TotalMembers = data.TotalMembers,
            ArchetypeCode = archetype.Code,
            ArchetypeEmoji = archetype.Emoji,
            ArchetypeTitle = archetype.Title,
            ArchetypeDescription = archetype.Description,
            ThemeColor = archetype.ThemeColor,
            Slides = slides,
            FinalCard = BuildFinalCard(data, archetype)
        };
    }

    private static WrappedFinalCardDto BuildFinalCard(WrappedSeasonData data, WrappedArchetypeResult archetype)
    {
        var member = data.Target;
        var hits = data.HitsOfTotal(member.MemberId);
        var played = data.PickCountOf(member.MemberId);
        var effectiveness = WrappedCopy.Percent(hits, played);
        var bestStreak = Math.Max(member.BestStreak, data.LongestStreak(member.MemberId));

        // Exactamente 4 estadisticas: la cuarta depende de la singularidad del miembro.
        var stats = new List<WrappedCardStatDto>
        {
            new() { Label = "Puntos", Value = member.TotalPoints.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            new() { Label = "Efectividad", Value = effectiveness },
            new() { Label = "Mejor racha", Value = bestStreak.ToString(System.Globalization.CultureInfo.InvariantCulture) }
        };

        if (member.TotalUpsets > 0)
        {
            stats.Add(new WrappedCardStatDto
            {
                Label = "Sorpresas",
                Value = member.TotalUpsets.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
        }
        else
        {
            stats.Add(new WrappedCardStatDto
            {
                Label = "Mejor jornada",
                Value = data.BestWeekHits(member.MemberId).ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
        }

        return new WrappedFinalCardDto
        {
            DisplayName = member.DisplayName,
            AvatarUrl = member.AvatarUrl,
            SeasonName = data.Input.SeasonName,
            FinalPosition = data.FinalPosition,
            TotalMembers = data.TotalMembers,
            ArchetypeEmoji = archetype.Emoji,
            ArchetypeTitle = archetype.Title,
            ThemeColor = archetype.ThemeColor,
            Stats = stats
        };
    }
}
