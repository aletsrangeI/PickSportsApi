using Domain.Entities;
using DTO.CustomEvents;
using FluentAssertions;
using UseCases.CustomEvents;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class CustomEventScoringEngineTests
{
    private readonly CustomEventScoringEngine _engine = new();

    [Fact]
    public void EvaluateRound_Hit_AssignsIsHitTrueAndPointsEarnedEqualsQuestionPoints()
    {
        // Arrange
        var question = CreateQuestion(1, points: 5, options: new[] { (1, "A"), (2, "B") });
        var resolution = new QuestionResolutionItemDto { EventQuestionId = 1, WinningOptionId = 2 };
        var pick = new EventPick
        {
            Id = 1,
            EventQuestionId = 1,
            EventOptionId = 2,
            QuinielaMemberId = 10
        };

        // Act
        _engine.EvaluateRound(
            new[] { question },
            new[] { resolution },
            new[] { pick });

        // Assert
        pick.IsHit.Should().BeTrue();
        pick.PointsEarned.Should().Be(5);
    }

    [Fact]
    public void EvaluateRound_Miss_AssignsIsHitFalseAndZeroPoints()
    {
        // Arrange
        var question = CreateQuestion(1, points: 3, options: new[] { (1, "A"), (2, "B") });
        var resolution = new QuestionResolutionItemDto { EventQuestionId = 1, WinningOptionId = 2 };
        var pick = new EventPick
        {
            Id = 1,
            EventQuestionId = 1,
            EventOptionId = 1,
            QuinielaMemberId = 10
        };

        // Act
        _engine.EvaluateRound(
            new[] { question },
            new[] { resolution },
            new[] { pick });

        // Assert
        pick.IsHit.Should().BeFalse();
        pick.PointsEarned.Should().Be(0);
    }

    [Fact]
    public void EvaluateRound_SetsIsCorrectOnOptions()
    {
        // Arrange
        var question = CreateQuestion(
            1,
            points: 1,
            options: new[] { (10, "Opción 1"), (20, "Opción 2"), (30, "Opción 3") });
        var resolution = new QuestionResolutionItemDto { EventQuestionId = 1, WinningOptionId = 20 };

        // Act
        _engine.EvaluateRound(
            new[] { question },
            new[] { resolution },
            Array.Empty<EventPick>());

        // Assert
        question.Options.Single(o => o.Id == 10).IsCorrect.Should().BeFalse();
        question.Options.Single(o => o.Id == 20).IsCorrect.Should().BeTrue();
        question.Options.Single(o => o.Id == 30).IsCorrect.Should().BeFalse();
    }

    [Fact]
    public void CalculateMemberStreaks_HitSequence_ReturnsCurrentAndBestStreak()
    {
        // Arrange
        var round = new EventRound { Id = 1, RoundOrder = 1 };
        var picks = new List<EventPick>
        {
            CreatePick(1, hit: true, round, questionOrder: 1),
            CreatePick(2, hit: true, round, questionOrder: 2),
            CreatePick(3, hit: true, round, questionOrder: 3),
            CreatePick(4, hit: false, round, questionOrder: 4),
            CreatePick(5, hit: true, round, questionOrder: 5),
            CreatePick(6, hit: true, round, questionOrder: 6)
        };

        // Act
        var (currentStreak, bestStreak) = _engine.CalculateMemberStreaks(picks);

        // Assert
        currentStreak.Should().Be(2);
        bestStreak.Should().Be(3);
    }

    [Fact]
    public void CalculateMemberStreaks_ResetAfterMiss_KeepsBestStreak()
    {
        // Arrange
        var round = new EventRound { Id = 1, RoundOrder = 1 };
        var picks = new List<EventPick>
        {
            CreatePick(1, hit: true, round, questionOrder: 1),
            CreatePick(2, hit: true, round, questionOrder: 2),
            CreatePick(3, hit: true, round, questionOrder: 3),
            CreatePick(4, hit: false, round, questionOrder: 4),
            CreatePick(5, hit: true, round, questionOrder: 5),
            CreatePick(6, hit: false, round, questionOrder: 6)
        };

        // Act
        var (currentStreak, bestStreak) = _engine.CalculateMemberStreaks(picks);

        // Assert
        currentStreak.Should().Be(0);
        bestStreak.Should().Be(3);
    }

    [Fact]
    public void CalculateRoundStandings_SortsByPointsDescending()
    {
        // Arrange
        var round = new EventRound { Id = 1, RoundOrder = 1 };
        var q1 = CreateQuestion(1, points: 2, round: round, options: new[] { (10, "A"), (20, "B") });

        var memberHigh = new QuinielaMember { Id = 1, Alias = "High" };
        var memberLow = new QuinielaMember { Id = 2, Alias = "Low" };

        var picks = new List<EventPick>
        {
            CreatePick(1, hit: true, q1, memberId: 1, pointsEarned: 2),
            CreatePick(2, hit: false, q1, memberId: 2, pointsEarned: 0)
        };

        // Act
        var standings = _engine.CalculateRoundStandings(
            new[] { memberHigh, memberLow },
            new[] { q1 },
            picks);

        // Assert
        standings.Should().HaveCount(2);
        standings[0].Alias.Should().Be("High");
        standings[0].Points.Should().Be(2);
        standings[0].Rank.Should().Be(1);
        standings[1].Alias.Should().Be("Low");
        standings[1].Points.Should().Be(0);
        standings[1].Rank.Should().Be(2);
    }

    [Fact]
    public void CalculateRoundStandings_Tiebreak_CascadePointsHitsBestStreakAlias()
    {
        // Arrange
        var round = new EventRound { Id = 1, RoundOrder = 1 };
        var q1 = CreateQuestion(1, points: 1, round: round, options: new[] { (10, "A"), (20, "B") });

        var memberZoe = new QuinielaMember { Id = 1, Alias = "Zoe" };
        var memberAna = new QuinielaMember { Id = 2, Alias = "Ana" };

        // Ambos: 1 punto, 1 acierto. Zoe tiene mejor racha histórica => gana el desempate.
        var picks = new List<EventPick>
        {
            CreatePick(1, hit: true, q1, memberId: 1, pointsEarned: 1),
            CreatePick(2, hit: true, q1, memberId: 2, pointsEarned: 1)
        };

        // Fuerza la mejor racha de Zoe a 2 para romper el empate
        memberZoe.BestStreak = 2;
        memberAna.BestStreak = 1;

        // Act
        var standings = _engine.CalculateRoundStandings(
            new[] { memberZoe, memberAna },
            new[] { q1 },
            picks);

        // Assert
        standings.Should().HaveCount(2);
        standings[0].Alias.Should().Be("Zoe");
        standings[0].BestStreak.Should().Be(2);
        standings[1].Alias.Should().Be("Ana");
        standings[1].BestStreak.Should().Be(1);
    }

    [Fact]
    public void CalculateRoundStandings_QuestionsWithoutPicks_HandlesCleanlyWithTotalQuestions()
    {
        // Arrange
        var round = new EventRound { Id = 1, RoundOrder = 1 };
        var q1 = CreateQuestion(1, points: 2, round: round, options: new[] { (10, "A") });
        var q2 = CreateQuestion(2, points: 3, round: round, options: new[] { (20, "B") });
        var q3 = CreateQuestion(3, points: 5, round: round, options: new[] { (30, "C") });

        var member = new QuinielaMember { Id = 1, Alias = "Solo" };

        // No picks at all
        var picks = Array.Empty<EventPick>();

        // Act
        var act = () => _engine.CalculateRoundStandings(
            new[] { member },
            new[] { q1, q2, q3 },
            picks);

        // Assert
        var standings = act.Should().NotThrow().Which;
        standings.Should().ContainSingle();
        standings[0].TotalQuestions.Should().Be(3);
        standings[0].Points.Should().Be(0);
        standings[0].Hits.Should().Be(0);
    }

    [Fact]
    public void CalculateGeneralStandings_AccumulatesMultipleMembersAcrossQuestions()
    {
        // Arrange
        var round = new EventRound { Id = 1, RoundOrder = 1 };
        var q1 = CreateQuestion(1, points: 2, round: round, options: new[] { (10, "A"), (20, "B") });
        var q2 = CreateQuestion(2, points: 3, round: round, options: new[] { (30, "C"), (40, "D") });

        var alex = new QuinielaMember { Id = 1, Alias = "Alex" };
        var fer = new QuinielaMember { Id = 2, Alias = "Fer" };

        var picks = new List<EventPick>
        {
            // Alex: 2 aciertos => 2 + 3 = 5 puntos
            CreatePick(1, hit: true, q1, memberId: 1, pointsEarned: 2),
            CreatePick(2, hit: true, q2, memberId: 1, pointsEarned: 3),

            // Fer: 1 acierto => 2 puntos
            CreatePick(3, hit: true, q1, memberId: 2, pointsEarned: 2),
            CreatePick(4, hit: false, q2, memberId: 2, pointsEarned: 0)
        };

        // Act
        var standings = _engine.CalculateGeneralStandings(
            new[] { alex, fer },
            picks,
            totalResolvedQuestions: 2);

        // Assert
        standings.Should().HaveCount(2);
        standings[0].Alias.Should().Be("Alex");
        standings[0].Points.Should().Be(5);
        standings[0].Hits.Should().Be(2);
        standings[0].TotalQuestions.Should().Be(2);
        standings[1].Alias.Should().Be("Fer");
        standings[1].Points.Should().Be(2);
        standings[1].Hits.Should().Be(1);
    }

    #region Helpers

    private static EventQuestion CreateQuestion(
        int id,
        int points = 1,
        EventRound? round = null,
        IEnumerable<(int Id, string Text)>? options = null)
    {
        var question = new EventQuestion
        {
            Id = id,
            QuestionText = $"Question {id}",
            Points = points,
            QuestionOrder = id,
            EventRound = round ?? new EventRound { Id = id, RoundOrder = id },
            EventRoundId = round?.Id ?? id
        };

        foreach (var (optionId, text) in options ?? Enumerable.Empty<(int, string)>())
        {
            question.Options.Add(new EventOption
            {
                Id = optionId,
                OptionText = text,
                EventQuestionId = id,
                EventQuestion = question
            });
        }

        return question;
    }

    private static EventPick CreatePick(
        int id,
        bool hit,
        EventQuestion question,
        int memberId = 1,
        int? pointsEarned = null,
        DateTime? submittedAtUtc = null)
    {
        var pick = new EventPick
        {
            Id = id,
            EventQuestionId = question.Id,
            EventQuestion = question,
            EventOptionId = question.Options.First().Id,
            QuinielaMemberId = memberId,
            IsHit = hit,
            PointsEarned = pointsEarned ?? (hit ? question.Points : 0),
            SubmittedAtUtc = submittedAtUtc ?? DateTime.UtcNow
        };

        question.Picks.Add(pick);
        return pick;
    }

    private static EventPick CreatePick(
        int id,
        bool hit,
        EventRound round,
        int questionOrder,
        int memberId = 1,
        DateTime? submittedAtUtc = null)
    {
        var question = new EventQuestion
        {
            Id = id * 100 + questionOrder,
            QuestionText = $"Q{questionOrder}",
            Points = 1,
            QuestionOrder = questionOrder,
            EventRound = round,
            EventRoundId = round.Id
        };

        var option = new EventOption
        {
            Id = question.Id * 10,
            OptionText = "Opt",
            EventQuestionId = question.Id,
            EventQuestion = question
        };
        question.Options.Add(option);

        var pick = new EventPick
        {
            Id = id,
            EventQuestionId = question.Id,
            EventQuestion = question,
            EventOptionId = option.Id,
            QuinielaMemberId = memberId,
            IsHit = hit,
            PointsEarned = hit ? 1 : 0,
            SubmittedAtUtc = submittedAtUtc ?? DateTime.UtcNow
        };

        question.Picks.Add(pick);
        return pick;
    }

    #endregion
}
