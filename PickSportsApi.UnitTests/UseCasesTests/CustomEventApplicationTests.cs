using Domain.Entities;
using DTO.CustomEvents;
using FluentAssertions;
using Interface.Persistence;
using Moq;
using UseCases.CustomEvents;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class CustomEventApplicationTests
{
    private const int QuinielaId = 1;
    private const int RoundId = 100;
    private const int UserId = 7;
    private const int MemberId = 70;

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IQuinielaRepository> _quinielas = new();
    private readonly Mock<IQuinielaMemberRepository> _members = new();
    private readonly Mock<IPickRepository> _sportsPicks = new();
    private readonly Mock<ICustomEventRepository> _events = new();

    private readonly CustomEventApplication _sut;

    public CustomEventApplicationTests()
    {
        _uow.Setup(u => u.Users).Returns(_users.Object);
        _uow.Setup(u => u.Quinielas).Returns(_quinielas.Object);
        _uow.Setup(u => u.QuinielaMembers).Returns(_members.Object);
        _uow.Setup(u => u.Picks).Returns(_sportsPicks.Object);
        _uow.Setup(u => u.CustomEvents).Returns(_events.Object);
        _uow.Setup(u => u.Save(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _sut = new CustomEventApplication(_uow.Object, new CustomEventScoringEngine());
    }

    #region Helpers

    private void ArrangeActiveQuiniela(bool isActive = true)
    {
        _quinielas.Setup(r => r.GetAsync(QuinielaId))
            .ReturnsAsync(new Quiniela { Id = QuinielaId, Name = "Quiniela Test", IsActive = isActive, Active = true });
    }

    private void ArrangeMissingQuiniela()
    {
        _quinielas.Setup(r => r.GetAsync(QuinielaId)).ReturnsAsync((Quiniela)null!);
    }

    private QuinielaMember ArrangeMembership(string? memberRole, string userRole = "USER", bool memberActive = true)
    {
        QuinielaMember? member = null;
        if (memberRole != null)
        {
            member = new QuinielaMember
            {
                Id = MemberId,
                QuinielaId = QuinielaId,
                UserId = UserId,
                Alias = "Yo",
                Role = memberRole,
                Active = memberActive
            };
        }

        _members.Setup(r => r.GetMembershipAsync(QuinielaId, UserId)).ReturnsAsync(member);
        _users.Setup(r => r.GetAsync(UserId))
            .ReturnsAsync(new User { Id = UserId, Username = "tester", Role = userRole, DisplayName = "Tester" });

        return member!;
    }

    private static EventRound BuildRound(
        string status = "Open",
        DateTime? lockDateUtc = null,
        int quinielaId = QuinielaId,
        bool active = true)
    {
        return new EventRound
        {
            Id = RoundId,
            EventGroupId = 5,
            Title = "Ronda 1",
            RoundOrder = 1,
            LockDateUtc = lockDateUtc ?? DateTime.UtcNow.AddDays(1),
            Status = status,
            Active = active,
            EventGroup = new EventGroup { Id = 5, QuinielaId = quinielaId, Title = "Eventos", Active = true }
        };
    }

    private static EventQuestion AddQuestion(
        EventRound round,
        int id,
        int points = 1,
        int firstOptionId = 0,
        int optionCount = 2,
        bool active = true)
    {
        int startId = firstOptionId == 0 ? id * 10 + 1 : firstOptionId;
        var question = new EventQuestion
        {
            Id = id,
            EventRoundId = round.Id,
            QuestionText = $"Pregunta {id}",
            Points = points,
            QuestionOrder = id,
            Active = active,
            EventRound = round
        };

        for (int i = 0; i < optionCount; i++)
        {
            question.Options.Add(new EventOption
            {
                Id = startId + i,
                EventQuestionId = id,
                OptionText = $"Opción {startId + i}",
                OptionOrder = i + 1,
                Active = true,
                EventQuestion = question
            });
        }

        round.Questions.Add(question);
        return question;
    }

    private static EventPick AddPick(
        EventQuestion question,
        int memberId,
        int optionId,
        string alias = "Participante X",
        bool active = true,
        bool? isHit = null,
        int pointsEarned = 0,
        DateTime? submittedAtUtc = null)
    {
        var option = question.Options.FirstOrDefault(o => o.Id == optionId);
        var pick = new EventPick
        {
            Id = question.Picks.Count + 1000 + memberId,
            EventQuestionId = question.Id,
            EventOptionId = optionId,
            QuinielaMemberId = memberId,
            QuinielaMember = new QuinielaMember
            {
                Id = memberId,
                Alias = alias,
                User = new User { Id = memberId, AvatarUrl = $"http://avatar/{memberId}.png" }
            },
            EventOption = option!,
            EventQuestion = question,
            SubmittedAtUtc = submittedAtUtc ?? DateTime.UtcNow.AddHours(-1),
            IsHit = isHit,
            PointsEarned = pointsEarned,
            Active = active
        };

        question.Picks.Add(pick);
        return pick;
    }

    private void ArrangeRoundWithDetails(EventRound? round)
    {
        _events.Setup(r => r.GetRoundByIdWithDetailsAsync(RoundId)).ReturnsAsync(round);
    }

    private void ArrangeRoundSimple(EventRound? round)
    {
        _events.Setup(r => r.GetRoundByIdAsync(RoundId)).ReturnsAsync(round);
    }

    #endregion

    #region GetRoundsAsync

    [Fact]
    public async Task GetRoundsAsync_QuinielaDoesNotExist_ReturnsFailure()
    {
        // Arrange
        ArrangeMissingQuiniela();

        // Act
        var result = await _sut.GetRoundsAsync(QuinielaId, UserId);

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("no existe o está inactiva");
        _events.Verify(r => r.GetRoundsByQuinielaIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetRoundsAsync_QuinielaInactive_ReturnsFailure()
    {
        // Arrange
        ArrangeActiveQuiniela(isActive: false);

        // Act
        var result = await _sut.GetRoundsAsync(QuinielaId, UserId);

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("no existe o está inactiva");
    }

    [Fact]
    public async Task GetRoundsAsync_UserIsNotMemberNorGlobalAdmin_ReturnsFailure()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership(memberRole: null);

        // Act
        var result = await _sut.GetRoundsAsync(QuinielaId, UserId);

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("No eres miembro");
        _events.Verify(r => r.GetRoundsByQuinielaIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetRoundsAsync_Member_ReturnsSummaryWithCountsAndUserPointsOnlyFromHits()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("MEMBER");

        var round = BuildRound();
        var q1 = AddQuestion(round, 1, points: 3);
        var q2 = AddQuestion(round, 2, points: 2);
        AddQuestion(round, 3, points: 10, active: false); // inactiva: no cuenta
        AddPick(q1, MemberId, q1.Options.First().Id, isHit: true, pointsEarned: 3);
        AddPick(q2, MemberId, q2.Options.First().Id, isHit: false, pointsEarned: 0);
        AddPick(q1, memberId: 999, q1.Options.First().Id, isHit: true, pointsEarned: 3); // otro miembro
        _events.Setup(r => r.GetRoundsByQuinielaIdAsync(QuinielaId)).ReturnsAsync(new[] { round });

        // Act
        var result = await _sut.GetRoundsAsync(QuinielaId, UserId);

        // Assert
        result.isSuccess.Should().BeTrue();
        var summary = result.Data.Should().ContainSingle().Subject;
        summary.Id.Should().Be(RoundId);
        summary.QuinielaId.Should().Be(QuinielaId);
        summary.QuestionsCount.Should().Be(2);
        summary.TotalPossiblePoints.Should().Be(5);
        summary.UserPicksCount.Should().Be(2);
        summary.UserPointsEarned.Should().Be(3);
    }

    [Fact]
    public async Task GetRoundsAsync_InactivePicksAreIgnoredInUserCounts()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("MEMBER");

        var round = BuildRound();
        var q1 = AddQuestion(round, 1, points: 4);
        AddPick(q1, MemberId, q1.Options.First().Id, active: false, isHit: true, pointsEarned: 4);
        _events.Setup(r => r.GetRoundsByQuinielaIdAsync(QuinielaId)).ReturnsAsync(new[] { round });

        // Act
        var result = await _sut.GetRoundsAsync(QuinielaId, UserId);

        // Assert
        var summary = result.Data.Should().ContainSingle().Subject;
        summary.UserPicksCount.Should().Be(0);
        summary.UserPointsEarned.Should().Be(0);
    }

    [Fact]
    public async Task GetRoundsAsync_GlobalAdminWithoutMembership_ReturnsRoundsWithZeroUserPicks()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership(memberRole: null, userRole: "ADMIN");

        var round = BuildRound();
        var q1 = AddQuestion(round, 1, points: 2);
        AddPick(q1, memberId: 999, q1.Options.First().Id, isHit: true, pointsEarned: 2);
        _events.Setup(r => r.GetRoundsByQuinielaIdAsync(QuinielaId)).ReturnsAsync(new[] { round });

        // Act
        var result = await _sut.GetRoundsAsync(QuinielaId, UserId);

        // Assert
        result.isSuccess.Should().BeTrue();
        var summary = result.Data.Should().ContainSingle().Subject;
        summary.UserPicksCount.Should().Be(0);
        summary.UserPointsEarned.Should().Be(0);
        summary.QuestionsCount.Should().Be(1);
    }

    [Fact]
    public async Task GetRoundsAsync_NoRounds_ReturnsEmptyList()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("OWNER");
        _events.Setup(r => r.GetRoundsByQuinielaIdAsync(QuinielaId)).ReturnsAsync(Array.Empty<EventRound>());

        // Act
        var result = await _sut.GetRoundsAsync(QuinielaId, UserId);

        // Assert
        result.isSuccess.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    #endregion

    #region GetRoundDetailAsync

    [Fact]
    public async Task GetRoundDetailAsync_QuinielaInactive_ReturnsFailure()
    {
        // Arrange
        ArrangeActiveQuiniela(isActive: false);

        // Act
        var result = await _sut.GetRoundDetailAsync(QuinielaId, RoundId, UserId);

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("no existe o está inactiva");
    }

    [Fact]
    public async Task GetRoundDetailAsync_UserIsNotMember_ReturnsFailure()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership(memberRole: null);

        // Act
        var result = await _sut.GetRoundDetailAsync(QuinielaId, RoundId, UserId);

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("No eres miembro");
    }

    [Fact]
    public async Task GetRoundDetailAsync_RoundNotFound_ReturnsFailure()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("MEMBER");
        ArrangeRoundWithDetails(null);

        // Act
        var result = await _sut.GetRoundDetailAsync(QuinielaId, RoundId, UserId);

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Ronda no encontrada");
    }

    [Fact]
    public async Task GetRoundDetailAsync_RoundBelongsToAnotherQuiniela_ReturnsFailure()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("MEMBER");
        ArrangeRoundWithDetails(BuildRound(quinielaId: 999));

        // Act
        var result = await _sut.GetRoundDetailAsync(QuinielaId, RoundId, UserId);

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Ronda no encontrada");
    }

    [Fact]
    public async Task GetRoundDetailAsync_InactiveRound_ReturnsFailure()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("MEMBER");
        ArrangeRoundWithDetails(BuildRound(active: false));

        // Act
        var result = await _sut.GetRoundDetailAsync(QuinielaId, RoundId, UserId);

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Ronda no encontrada");
    }

    [Fact]
    public async Task GetRoundDetailAsync_RoundNotLocked_HidesAllMemberPicks()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("MEMBER");
        var round = BuildRound(status: "Open", lockDateUtc: DateTime.UtcNow.AddDays(1));
        var q1 = AddQuestion(round, 1);
        AddPick(q1, memberId: 200, q1.Options.ElementAt(0).Id, alias: "Ana");
        AddPick(q1, memberId: 201, q1.Options.ElementAt(1).Id, alias: "Beto");
        ArrangeRoundWithDetails(round);

        // Act
        var result = await _sut.GetRoundDetailAsync(QuinielaId, RoundId, UserId);

        // Assert
        result.isSuccess.Should().BeTrue();
        result.Data.IsLocked.Should().BeFalse();
        result.Data.AllMemberPicks.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Locked")]
    [InlineData("Resolved")]
    public async Task GetRoundDetailAsync_RoundLockedByStatus_RevealsAllMemberPicks(string status)
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("MEMBER");
        var round = BuildRound(status: status, lockDateUtc: DateTime.UtcNow.AddDays(1));
        var q1 = AddQuestion(round, 1);
        AddPick(q1, memberId: 200, q1.Options.ElementAt(0).Id, alias: "Ana", isHit: true, pointsEarned: 1);
        AddPick(q1, memberId: 201, q1.Options.ElementAt(1).Id, alias: "Beto", isHit: false);
        ArrangeRoundWithDetails(round);

        // Act
        var result = await _sut.GetRoundDetailAsync(QuinielaId, RoundId, UserId);

        // Assert
        result.isSuccess.Should().BeTrue();
        result.Data.IsLocked.Should().BeTrue();
        result.Data.IsRevealed.Should().BeTrue();
        result.Data.AllMemberPicks.Should().HaveCount(2);
        result.Data.AllMemberPicks.Select(p => p.MemberAlias).Should().BeEquivalentTo("Ana", "Beto");
        var ana = result.Data.AllMemberPicks.Single(p => p.MemberAlias == "Ana");
        ana.MemberId.Should().Be(200);
        ana.OptionText.Should().Be(q1.Options.ElementAt(0).OptionText);
        ana.AvatarUrl.Should().Be("http://avatar/200.png");
        ana.IsHit.Should().BeTrue();
        ana.PointsEarned.Should().Be(1);
    }

    [Fact]
    public async Task GetRoundDetailAsync_LockDatePassed_RevealsAllMemberPicksEvenIfStatusOpen()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("MEMBER");
        var round = BuildRound(status: "Open", lockDateUtc: DateTime.UtcNow.AddMinutes(-5));
        var q1 = AddQuestion(round, 1);
        AddPick(q1, memberId: 200, q1.Options.ElementAt(0).Id, alias: "Ana");
        ArrangeRoundWithDetails(round);

        // Act
        var result = await _sut.GetRoundDetailAsync(QuinielaId, RoundId, UserId);

        // Assert
        result.Data.IsLocked.Should().BeTrue();
        result.Data.AllMemberPicks.Should().ContainSingle().Which.MemberAlias.Should().Be("Ana");
    }

    [Fact]
    public async Task GetRoundDetailAsync_LockedRound_ExcludesInactivePicksFromReveal()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("MEMBER");
        var round = BuildRound(status: "Locked");
        var q1 = AddQuestion(round, 1);
        AddPick(q1, memberId: 200, q1.Options.ElementAt(0).Id, alias: "Ana");
        AddPick(q1, memberId: 201, q1.Options.ElementAt(0).Id, alias: "Fantasma", active: false);
        ArrangeRoundWithDetails(round);

        // Act
        var result = await _sut.GetRoundDetailAsync(QuinielaId, RoundId, UserId);

        // Assert
        result.Data.AllMemberPicks.Should().ContainSingle().Which.MemberAlias.Should().Be("Ana");
    }

    [Fact]
    public async Task GetRoundDetailAsync_NotLocked_OnlyUserOwnPickIsCountedAndPercentagesAreZero()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("MEMBER");
        var round = BuildRound(status: "Open");
        var q1 = AddQuestion(round, 1);
        AddPick(q1, MemberId, q1.Options.ElementAt(0).Id);
        AddPick(q1, memberId: 200, q1.Options.ElementAt(1).Id);
        AddPick(q1, memberId: 201, q1.Options.ElementAt(1).Id);
        ArrangeRoundWithDetails(round);

        // Act
        var result = await _sut.GetRoundDetailAsync(QuinielaId, RoundId, UserId);

        // Assert
        var question = result.Data.Questions.Should().ContainSingle().Subject;
        question.UserSelectedOptionId.Should().Be(q1.Options.ElementAt(0).Id);
        question.Options.Single(o => o.Id == q1.Options.ElementAt(0).Id).PicksCount.Should().Be(1);
        question.Options.Single(o => o.Id == q1.Options.ElementAt(1).Id).PicksCount.Should().Be(0);
        question.Options.Should().OnlyContain(o => o.PickPercentage == 0m);
        result.Data.CurrentUserMemberId.Should().Be(MemberId);
    }

    [Fact]
    public async Task GetRoundDetailAsync_Locked_ComputesPicksCountAndPercentagePerOption()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("MEMBER");
        var round = BuildRound(status: "Locked");
        var q1 = AddQuestion(round, 1, optionCount: 3);
        AddPick(q1, memberId: 200, q1.Options.ElementAt(0).Id);
        AddPick(q1, memberId: 201, q1.Options.ElementAt(0).Id);
        AddPick(q1, memberId: 202, q1.Options.ElementAt(1).Id);
        ArrangeRoundWithDetails(round);

        // Act
        var result = await _sut.GetRoundDetailAsync(QuinielaId, RoundId, UserId);

        // Assert
        var options = result.Data.Questions.Single().Options;
        options.Single(o => o.Id == q1.Options.ElementAt(0).Id).PicksCount.Should().Be(2);
        options.Single(o => o.Id == q1.Options.ElementAt(0).Id).PickPercentage.Should().Be(66.7m);
        options.Single(o => o.Id == q1.Options.ElementAt(1).Id).PicksCount.Should().Be(1);
        options.Single(o => o.Id == q1.Options.ElementAt(1).Id).PickPercentage.Should().Be(33.3m);
        options.Single(o => o.Id == q1.Options.ElementAt(2).Id).PicksCount.Should().Be(0);
        options.Single(o => o.Id == q1.Options.ElementAt(2).Id).PickPercentage.Should().Be(0m);
    }

    [Fact]
    public async Task GetRoundDetailAsync_Locked_QuestionWithoutPicksHasZeroPercentages()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("MEMBER");
        var round = BuildRound(status: "Locked");
        AddQuestion(round, 1);
        ArrangeRoundWithDetails(round);

        // Act
        var result = await _sut.GetRoundDetailAsync(QuinielaId, RoundId, UserId);

        // Assert
        result.Data.Questions.Single().Options.Should().OnlyContain(o => o.PickPercentage == 0m && o.PicksCount == 0);
        result.Data.AllMemberPicks.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRoundDetailAsync_OpenRoundAsRegularMember_HidesIsCorrect()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("MEMBER");
        var round = BuildRound(status: "Open");
        var q1 = AddQuestion(round, 1);
        q1.Options.ElementAt(0).IsCorrect = true;
        ArrangeRoundWithDetails(round);

        // Act
        var result = await _sut.GetRoundDetailAsync(QuinielaId, RoundId, UserId);

        // Assert
        result.Data.IsOwnerOrAdmin.Should().BeFalse();
        result.Data.Questions.Single().Options.Should().OnlyContain(o => o.IsCorrect == null);
    }

    [Theory]
    [InlineData("OWNER")]
    [InlineData("ADMIN")]
    public async Task GetRoundDetailAsync_OpenRoundAsOwnerOrAdmin_ShowsIsCorrect(string role)
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership(role);
        var round = BuildRound(status: "Open");
        var q1 = AddQuestion(round, 1);
        q1.Options.ElementAt(0).IsCorrect = true;
        q1.Options.ElementAt(1).IsCorrect = false;
        ArrangeRoundWithDetails(round);

        // Act
        var result = await _sut.GetRoundDetailAsync(QuinielaId, RoundId, UserId);

        // Assert
        result.Data.IsOwnerOrAdmin.Should().BeTrue();
        var options = result.Data.Questions.Single().Options;
        options.Single(o => o.Id == q1.Options.ElementAt(0).Id).IsCorrect.Should().BeTrue();
        options.Single(o => o.Id == q1.Options.ElementAt(1).Id).IsCorrect.Should().BeFalse();
    }

    [Fact]
    public async Task GetRoundDetailAsync_ResolvedRoundAsRegularMember_ShowsIsCorrect()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("MEMBER");
        var round = BuildRound(status: "Resolved");
        var q1 = AddQuestion(round, 1);
        q1.Options.ElementAt(0).IsCorrect = true;
        q1.Options.ElementAt(1).IsCorrect = false;
        ArrangeRoundWithDetails(round);

        // Act
        var result = await _sut.GetRoundDetailAsync(QuinielaId, RoundId, UserId);

        // Assert
        var options = result.Data.Questions.Single().Options;
        options.Single(o => o.Id == q1.Options.ElementAt(0).Id).IsCorrect.Should().BeTrue();
        options.Single(o => o.Id == q1.Options.ElementAt(1).Id).IsCorrect.Should().BeFalse();
    }

    [Fact]
    public async Task GetRoundDetailAsync_GlobalAdminWithoutMembership_SucceedsAsOwnerOrAdminWithoutCurrentMember()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership(memberRole: null, userRole: "ADMIN");
        var round = BuildRound(status: "Open");
        AddQuestion(round, 1);
        ArrangeRoundWithDetails(round);

        // Act
        var result = await _sut.GetRoundDetailAsync(QuinielaId, RoundId, UserId);

        // Assert
        result.isSuccess.Should().BeTrue();
        result.Data.IsOwnerOrAdmin.Should().BeTrue();
        result.Data.CurrentUserMemberId.Should().BeNull();
    }

    [Fact]
    public async Task GetRoundDetailAsync_ExcludesInactiveQuestionsAndOptions()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("MEMBER");
        var round = BuildRound();
        var q1 = AddQuestion(round, 1, optionCount: 3);
        q1.Options.ElementAt(2).Active = false;
        AddQuestion(round, 2, active: false);
        ArrangeRoundWithDetails(round);

        // Act
        var result = await _sut.GetRoundDetailAsync(QuinielaId, RoundId, UserId);

        // Assert
        var question = result.Data.Questions.Should().ContainSingle().Subject;
        question.Id.Should().Be(1);
        question.Options.Should().HaveCount(2);
    }

    #endregion

    #region CreateRoundAsync

    [Fact]
    public async Task CreateRoundAsync_QuinielaDoesNotExist_ReturnsFailure()
    {
        // Arrange
        ArrangeMissingQuiniela();

        // Act
        var result = await _sut.CreateRoundAsync(QuinielaId, UserId,
            new CreateEventRoundDto { Title = "R", LockDateUtc = DateTime.UtcNow.AddDays(1) });

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("no existe o está inactiva");
        _events.Verify(r => r.InsertRoundAsync(It.IsAny<EventRound>()), Times.Never);
    }

    [Theory]
    [InlineData("MEMBER")]
    [InlineData(null)]
    public async Task CreateRoundAsync_NotOwnerNorAdmin_IsRejected(string? memberRole)
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership(memberRole);

        // Act
        var result = await _sut.CreateRoundAsync(QuinielaId, UserId,
            new CreateEventRoundDto { Title = "Ronda", LockDateUtc = DateTime.UtcNow.AddDays(1) });

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Solo el creador o administrador");
        _events.Verify(r => r.InsertRoundAsync(It.IsAny<EventRound>()), Times.Never);
    }

    [Theory]
    [InlineData("OWNER", "USER")]
    [InlineData("ADMIN", "USER")]
    public async Task CreateRoundAsync_OwnerOrAdminMember_CreatesRound(string memberRole, string userRole)
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership(memberRole, userRole);
        ArrangeGroupAndOrder(maxOrder: 0);
        _events.Setup(r => r.InsertRoundAsync(It.IsAny<EventRound>())).ReturnsAsync(true);

        // Act
        var result = await _sut.CreateRoundAsync(QuinielaId, UserId,
            new CreateEventRoundDto { Title = "Ronda 1", LockDateUtc = DateTime.UtcNow.AddDays(1) });

        // Assert
        result.isSuccess.Should().BeTrue();
        result.Data.Title.Should().Be("Ronda 1");
        result.Data.Status.Should().Be("Open");
        result.Data.QuinielaId.Should().Be(QuinielaId);
        result.Data.QuestionsCount.Should().Be(0);
        _events.Verify(r => r.InsertRoundAsync(It.IsAny<EventRound>()), Times.Once);
    }

    [Fact]
    public async Task CreateRoundAsync_GlobalAdminWithoutMembership_CreatesRound()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership(memberRole: null, userRole: "ADMIN");
        ArrangeGroupAndOrder(maxOrder: 0);
        _events.Setup(r => r.InsertRoundAsync(It.IsAny<EventRound>())).ReturnsAsync(true);

        // Act
        var result = await _sut.CreateRoundAsync(QuinielaId, UserId,
            new CreateEventRoundDto { Title = "Ronda Admin", LockDateUtc = DateTime.UtcNow.AddDays(1) });

        // Assert
        result.isSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateRoundAsync_EmptyTitle_IsRejected(string? title)
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("OWNER");

        // Act
        var result = await _sut.CreateRoundAsync(QuinielaId, UserId,
            new CreateEventRoundDto { Title = title!, LockDateUtc = DateTime.UtcNow.AddDays(1) });

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("título de la ronda es obligatorio");
        _events.Verify(r => r.InsertRoundAsync(It.IsAny<EventRound>()), Times.Never);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(4, 5)]
    public async Task CreateRoundAsync_WithoutExplicitOrder_UsesMaxOrderPlusOne(int maxOrder, int expectedOrder)
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("OWNER");
        ArrangeGroupAndOrder(maxOrder);
        EventRound? inserted = null;
        _events.Setup(r => r.InsertRoundAsync(It.IsAny<EventRound>()))
            .Callback<EventRound>(r => inserted = r)
            .ReturnsAsync(true);

        // Act
        var result = await _sut.CreateRoundAsync(QuinielaId, UserId,
            new CreateEventRoundDto { Title = "Ronda", LockDateUtc = DateTime.UtcNow.AddDays(1), RoundOrder = null });

        // Assert
        result.isSuccess.Should().BeTrue();
        result.Data.RoundOrder.Should().Be(expectedOrder);
        inserted.Should().NotBeNull();
        inserted!.RoundOrder.Should().Be(expectedOrder);
        _events.Verify(r => r.GetMaxRoundOrderByQuinielaIdAsync(QuinielaId), Times.Once);
    }

    [Fact]
    public async Task CreateRoundAsync_WithExplicitOrder_UsesRequestedOrder()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("OWNER");
        ArrangeGroupAndOrder(maxOrder: 4);
        _events.Setup(r => r.InsertRoundAsync(It.IsAny<EventRound>())).ReturnsAsync(true);

        // Act
        var result = await _sut.CreateRoundAsync(QuinielaId, UserId,
            new CreateEventRoundDto { Title = "Ronda", LockDateUtc = DateTime.UtcNow.AddDays(1), RoundOrder = 12 });

        // Assert
        result.isSuccess.Should().BeTrue();
        result.Data.RoundOrder.Should().Be(12);
    }

    [Fact]
    public async Task CreateRoundAsync_PersistsTrimmedTitleUtcLockDateOpenStatusAndAudit()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("OWNER");
        ArrangeGroupAndOrder(maxOrder: 2, groupId: 55);
        EventRound? persisted = null;
        _events.Setup(r => r.InsertRoundAsync(It.IsAny<EventRound>()))
            .Callback<EventRound>(r => persisted = r)
            .ReturnsAsync(true);
        var unspecifiedDate = new DateTime(2030, 1, 15, 18, 30, 0, DateTimeKind.Unspecified);

        // Act
        var result = await _sut.CreateRoundAsync(QuinielaId, UserId,
            new CreateEventRoundDto { Title = "  Octavos  ", LockDateUtc = unspecifiedDate });

        // Assert
        result.isSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.Title.Should().Be("Octavos");
        persisted.EventGroupId.Should().Be(55);
        persisted.RoundOrder.Should().Be(3);
        persisted.Status.Should().Be("Open");
        persisted.Active.Should().BeTrue();
        persisted.LockDateUtc.Kind.Should().Be(DateTimeKind.Utc);
        persisted.LockDateUtc.Should().Be(new DateTime(2030, 1, 15, 18, 30, 0, DateTimeKind.Utc));
        persisted.CreatedBy.Should().Be("tester");
        result.Data.EventGroupId.Should().Be(55);
    }

    [Fact]
    public async Task CreateRoundAsync_UsesQuinielaNameAsDefaultGroupTitle()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("OWNER");
        ArrangeGroupAndOrder(maxOrder: 0);
        _events.Setup(r => r.InsertRoundAsync(It.IsAny<EventRound>())).ReturnsAsync(true);

        // Act
        await _sut.CreateRoundAsync(QuinielaId, UserId,
            new CreateEventRoundDto { Title = "Ronda", LockDateUtc = DateTime.UtcNow.AddDays(1) });

        // Assert
        _events.Verify(r => r.GetOrCreateEventGroupForQuinielaAsync(QuinielaId, "Quiniela Test"), Times.Once);
    }

    [Fact]
    public async Task CreateRoundAsync_InsertFails_ReturnsFailure()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("OWNER");
        ArrangeGroupAndOrder(maxOrder: 0);
        _events.Setup(r => r.InsertRoundAsync(It.IsAny<EventRound>())).ReturnsAsync(false);

        // Act
        var result = await _sut.CreateRoundAsync(QuinielaId, UserId,
            new CreateEventRoundDto { Title = "Ronda", LockDateUtc = DateTime.UtcNow.AddDays(1) });

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error al crear la ronda");
        result.Data.Should().BeNull();
    }

    private void ArrangeGroupAndOrder(int maxOrder, int groupId = 5)
    {
        _events.Setup(r => r.GetOrCreateEventGroupForQuinielaAsync(QuinielaId, It.IsAny<string>()))
            .ReturnsAsync(new EventGroup { Id = groupId, QuinielaId = QuinielaId, Title = "Eventos" });
        _events.Setup(r => r.GetMaxRoundOrderByQuinielaIdAsync(QuinielaId)).ReturnsAsync(maxOrder);
    }

    #endregion

    #region UpdateRoundAsync

    [Theory]
    [InlineData("MEMBER")]
    [InlineData(null)]
    public async Task UpdateRoundAsync_NotOwnerNorAdmin_IsRejected(string? memberRole)
    {
        // Arrange
        ArrangeMembership(memberRole);

        // Act
        var result = await _sut.UpdateRoundAsync(QuinielaId, RoundId, UserId,
            new UpdateEventRoundDto { Title = "Nuevo", LockDateUtc = DateTime.UtcNow.AddDays(2) });

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Solo el creador o administrador");
        _events.Verify(r => r.UpdateRoundAsync(It.IsAny<EventRound>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRoundAsync_RoundNotFound_ReturnsFailure()
    {
        // Arrange
        ArrangeMembership("OWNER");
        ArrangeRoundSimple(null);

        // Act
        var result = await _sut.UpdateRoundAsync(QuinielaId, RoundId, UserId,
            new UpdateEventRoundDto { Title = "Nuevo", LockDateUtc = DateTime.UtcNow.AddDays(2) });

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Ronda no encontrada");
    }

    [Fact]
    public async Task UpdateRoundAsync_RoundBelongsToAnotherQuiniela_ReturnsFailure()
    {
        // Arrange
        ArrangeMembership("OWNER");
        ArrangeRoundSimple(BuildRound(quinielaId: 999));

        // Act
        var result = await _sut.UpdateRoundAsync(QuinielaId, RoundId, UserId,
            new UpdateEventRoundDto { Title = "Nuevo", LockDateUtc = DateTime.UtcNow.AddDays(2) });

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Ronda no encontrada");
        _events.Verify(r => r.UpdateRoundAsync(It.IsAny<EventRound>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRoundAsync_RoundAlreadyResolved_IsRejectedWithoutPersisting()
    {
        // Arrange
        ArrangeMembership("OWNER");
        var round = BuildRound(status: "Resolved");
        ArrangeRoundSimple(round);

        // Act
        var result = await _sut.UpdateRoundAsync(QuinielaId, RoundId, UserId,
            new UpdateEventRoundDto { Title = "Cambio", LockDateUtc = DateTime.UtcNow.AddDays(2), Status = "Open" });

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("ya resuelta");
        round.Title.Should().Be("Ronda 1");
        round.Status.Should().Be("Resolved");
        _events.Verify(r => r.UpdateRoundAsync(It.IsAny<EventRound>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRoundAsync_ValidRequest_UpdatesTitleLockDateStatusAndAudit()
    {
        // Arrange
        ArrangeMembership("ADMIN");
        var round = BuildRound(status: "Open");
        ArrangeRoundSimple(round);
        _events.Setup(r => r.UpdateRoundAsync(round)).ReturnsAsync(true);
        var newLock = new DateTime(2031, 3, 1, 10, 0, 0, DateTimeKind.Unspecified);

        // Act
        var result = await _sut.UpdateRoundAsync(QuinielaId, RoundId, UserId,
            new UpdateEventRoundDto { Title = "  Cuartos  ", LockDateUtc = newLock, Status = " Locked " });

        // Assert
        result.isSuccess.Should().BeTrue();
        round.Title.Should().Be("Cuartos");
        round.Status.Should().Be("Locked");
        round.LockDateUtc.Kind.Should().Be(DateTimeKind.Utc);
        round.LockDateUtc.Should().Be(new DateTime(2031, 3, 1, 10, 0, 0, DateTimeKind.Utc));
        round.LastModifiedBy.Should().Be("tester");
        round.LastModified.Should().NotBeNull();
        result.Data.Title.Should().Be("Cuartos");
        result.Data.Status.Should().Be("Locked");
        _events.Verify(r => r.UpdateRoundAsync(round), Times.Once);
    }

    [Fact]
    public async Task UpdateRoundAsync_BlankTitleAndStatus_KeepsExistingValues()
    {
        // Arrange
        ArrangeMembership("OWNER");
        var round = BuildRound(status: "Open");
        ArrangeRoundSimple(round);
        _events.Setup(r => r.UpdateRoundAsync(round)).ReturnsAsync(true);

        // Act
        var result = await _sut.UpdateRoundAsync(QuinielaId, RoundId, UserId,
            new UpdateEventRoundDto { Title = "  ", LockDateUtc = DateTime.UtcNow.AddDays(3), Status = null });

        // Assert
        result.isSuccess.Should().BeTrue();
        round.Title.Should().Be("Ronda 1");
        round.Status.Should().Be("Open");
    }

    [Fact]
    public async Task UpdateRoundAsync_GlobalAdminWithoutMembership_CanUpdate()
    {
        // Arrange
        ArrangeMembership(memberRole: null, userRole: "ADMIN");
        var round = BuildRound();
        ArrangeRoundSimple(round);
        _events.Setup(r => r.UpdateRoundAsync(round)).ReturnsAsync(true);

        // Act
        var result = await _sut.UpdateRoundAsync(QuinielaId, RoundId, UserId,
            new UpdateEventRoundDto { Title = "Admin", LockDateUtc = DateTime.UtcNow.AddDays(3) });

        // Assert
        result.isSuccess.Should().BeTrue();
        round.Title.Should().Be("Admin");
    }

    [Fact]
    public async Task UpdateRoundAsync_PersistenceFails_ReturnsFailure()
    {
        // Arrange
        ArrangeMembership("OWNER");
        var round = BuildRound();
        ArrangeRoundSimple(round);
        _events.Setup(r => r.UpdateRoundAsync(round)).ReturnsAsync(false);

        // Act
        var result = await _sut.UpdateRoundAsync(QuinielaId, RoundId, UserId,
            new UpdateEventRoundDto { Title = "X", LockDateUtc = DateTime.UtcNow.AddDays(3) });

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error al actualizar la ronda");
    }

    #endregion

    #region AddQuestionAsync

    private static CreateEventQuestionDto BuildQuestionRequest(int optionCount = 2, string text = "¿Quién gana?")
    {
        var dto = new CreateEventQuestionDto { QuestionText = text, Points = 2 };
        for (int i = 1; i <= optionCount; i++)
        {
            dto.Options.Add(new CreateEventOptionItemDto { OptionText = $"Opción {i}" });
        }

        return dto;
    }

    [Theory]
    [InlineData("MEMBER")]
    [InlineData(null)]
    public async Task AddQuestionAsync_NotOwnerNorAdmin_IsRejected(string? memberRole)
    {
        // Arrange
        ArrangeMembership(memberRole);

        // Act
        var result = await _sut.AddQuestionAsync(QuinielaId, RoundId, UserId, BuildQuestionRequest());

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Solo el creador o administrador");
        _events.Verify(r => r.InsertQuestionAsync(It.IsAny<EventQuestion>()), Times.Never);
    }

    [Fact]
    public async Task AddQuestionAsync_RoundNotFound_ReturnsFailure()
    {
        // Arrange
        ArrangeMembership("OWNER");
        ArrangeRoundSimple(null);

        // Act
        var result = await _sut.AddQuestionAsync(QuinielaId, RoundId, UserId, BuildQuestionRequest());

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Ronda no encontrada");
    }

    [Fact]
    public async Task AddQuestionAsync_RoundAlreadyResolved_IsRejected()
    {
        // Arrange
        ArrangeMembership("OWNER");
        ArrangeRoundSimple(BuildRound(status: "Resolved"));

        // Act
        var result = await _sut.AddQuestionAsync(QuinielaId, RoundId, UserId, BuildQuestionRequest());

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("ya resuelta");
        _events.Verify(r => r.InsertQuestionAsync(It.IsAny<EventQuestion>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task AddQuestionAsync_EmptyQuestionText_IsRejected(string text)
    {
        // Arrange
        ArrangeMembership("OWNER");
        ArrangeRoundSimple(BuildRound());

        // Act
        var result = await _sut.AddQuestionAsync(QuinielaId, RoundId, UserId, BuildQuestionRequest(text: text));

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("texto de la pregunta es obligatorio");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task AddQuestionAsync_LessThanTwoOptions_IsRejected(int optionCount)
    {
        // Arrange
        ArrangeMembership("OWNER");
        ArrangeRoundSimple(BuildRound());

        // Act
        var result = await _sut.AddQuestionAsync(QuinielaId, RoundId, UserId, BuildQuestionRequest(optionCount));

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("al menos 2 opciones");
        _events.Verify(r => r.InsertQuestionAsync(It.IsAny<EventQuestion>()), Times.Never);
    }

    [Fact]
    public async Task AddQuestionAsync_NullOptions_IsRejected()
    {
        // Arrange
        ArrangeMembership("OWNER");
        ArrangeRoundSimple(BuildRound());
        var request = BuildQuestionRequest();
        request.Options = null!;

        // Act
        var result = await _sut.AddQuestionAsync(QuinielaId, RoundId, UserId, request);

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("al menos 2 opciones");
    }

    [Fact]
    public async Task AddQuestionAsync_OptionWithBlankText_IsRejected()
    {
        // Arrange
        ArrangeMembership("OWNER");
        ArrangeRoundSimple(BuildRound());
        var request = BuildQuestionRequest();
        request.Options[1].OptionText = "   ";

        // Act
        var result = await _sut.AddQuestionAsync(QuinielaId, RoundId, UserId, request);

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("opciones deben tener texto");
        _events.Verify(r => r.InsertQuestionAsync(It.IsAny<EventQuestion>()), Times.Never);
    }

    [Fact]
    public async Task AddQuestionAsync_ExactlyTwoOptions_InsertsQuestionWithNormalizedFields()
    {
        // Arrange
        ArrangeMembership("OWNER");
        ArrangeRoundSimple(BuildRound());
        _events.Setup(r => r.GetMaxQuestionOrderByRoundIdAsync(RoundId)).ReturnsAsync(3);
        EventQuestion? persisted = null;
        _events.Setup(r => r.InsertQuestionAsync(It.IsAny<EventQuestion>()))
            .Callback<EventQuestion>(q => persisted = q)
            .ReturnsAsync(true);
        var request = new CreateEventQuestionDto
        {
            QuestionText = "  ¿Quién gana?  ",
            Description = "  Final  ",
            Points = 5,
            QuestionType = " SingleChoice ",
            Options =
            {
                new CreateEventOptionItemDto { OptionText = " Local ", ImageUrl = " http://img/a.png " },
                new CreateEventOptionItemDto { OptionText = "Visita" }
            }
        };

        // Act
        var result = await _sut.AddQuestionAsync(QuinielaId, RoundId, UserId, request);

        // Assert
        result.isSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.EventRoundId.Should().Be(RoundId);
        persisted.QuestionText.Should().Be("¿Quién gana?");
        persisted.Description.Should().Be("Final");
        persisted.Points.Should().Be(5);
        persisted.QuestionType.Should().Be("SingleChoice");
        persisted.QuestionOrder.Should().Be(4);
        persisted.Active.Should().BeTrue();
        persisted.CreatedBy.Should().Be("tester");
        persisted.Options.Should().HaveCount(2);
        persisted.Options.Select(o => o.OptionText).Should().ContainInOrder("Local", "Visita");
        persisted.Options.First().ImageUrl.Should().Be("http://img/a.png");
        persisted.Options.Select(o => o.OptionOrder).Should().ContainInOrder(1, 2);
        persisted.Options.Should().OnlyContain(o => o.Active);
        result.Data.Options.Should().HaveCount(2);
        result.Data.Options.Should().OnlyContain(o => o.IsCorrect == null && o.PicksCount == 0);
    }

    [Fact]
    public async Task AddQuestionAsync_InvalidPointsAndBlankType_FallBackToDefaults()
    {
        // Arrange
        ArrangeMembership("OWNER");
        ArrangeRoundSimple(BuildRound());
        EventQuestion? persisted = null;
        _events.Setup(r => r.InsertQuestionAsync(It.IsAny<EventQuestion>()))
            .Callback<EventQuestion>(q => persisted = q)
            .ReturnsAsync(true);
        var request = BuildQuestionRequest();
        request.Points = 0;
        request.QuestionType = "";

        // Act
        var result = await _sut.AddQuestionAsync(QuinielaId, RoundId, UserId, request);

        // Assert
        result.isSuccess.Should().BeTrue();
        persisted!.Points.Should().Be(1);
        persisted.QuestionType.Should().Be("SingleChoice");
    }

    [Fact]
    public async Task AddQuestionAsync_ExplicitQuestionOrder_IsRespected()
    {
        // Arrange
        ArrangeMembership("OWNER");
        ArrangeRoundSimple(BuildRound());
        _events.Setup(r => r.GetMaxQuestionOrderByRoundIdAsync(RoundId)).ReturnsAsync(3);
        _events.Setup(r => r.InsertQuestionAsync(It.IsAny<EventQuestion>())).ReturnsAsync(true);
        var request = BuildQuestionRequest();
        request.QuestionOrder = 9;

        // Act
        var result = await _sut.AddQuestionAsync(QuinielaId, RoundId, UserId, request);

        // Assert
        result.Data.QuestionOrder.Should().Be(9);
    }

    [Fact]
    public async Task AddQuestionAsync_ExplicitOptionOrder_IsKeptAndMissingOnesAreSequential()
    {
        // Arrange
        ArrangeMembership("OWNER");
        ArrangeRoundSimple(BuildRound());
        EventQuestion? persisted = null;
        _events.Setup(r => r.InsertQuestionAsync(It.IsAny<EventQuestion>()))
            .Callback<EventQuestion>(q => persisted = q)
            .ReturnsAsync(true);
        var request = BuildQuestionRequest(optionCount: 3);
        request.Options[0].OptionOrder = 7;

        // Act
        await _sut.AddQuestionAsync(QuinielaId, RoundId, UserId, request);

        // Assert
        persisted!.Options.Select(o => o.OptionOrder).Should().ContainInOrder(7, 1, 2);
    }

    [Fact]
    public async Task AddQuestionAsync_InsertFails_ReturnsFailure()
    {
        // Arrange
        ArrangeMembership("OWNER");
        ArrangeRoundSimple(BuildRound());
        _events.Setup(r => r.InsertQuestionAsync(It.IsAny<EventQuestion>())).ReturnsAsync(false);

        // Act
        var result = await _sut.AddQuestionAsync(QuinielaId, RoundId, UserId, BuildQuestionRequest());

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error al guardar la pregunta");
    }

    #endregion

    #region SubmitPicksAsync

    private (EventRound round, EventQuestion q1, EventQuestion q2) ArrangeSubmitScenario(
        string status = "Open",
        DateTime? lockDateUtc = null)
    {
        ArrangeActiveQuiniela();
        ArrangeMembership("MEMBER");
        var round = BuildRound(status, lockDateUtc);
        var q1 = AddQuestion(round, 1);
        var q2 = AddQuestion(round, 2);
        ArrangeRoundWithDetails(round);
        return (round, q1, q2);
    }

    private static SubmitEventPicksDto Picks(params (int question, int option)[] picks)
    {
        return new SubmitEventPicksDto
        {
            Picks = picks.Select(p => new EventPickSelectionDto { EventQuestionId = p.question, EventOptionId = p.option }).ToList()
        };
    }

    [Theory]
    [InlineData(null)]
    [InlineData("MEMBER")]
    public async Task SubmitPicksAsync_MemberNullOrInactive_IsRejected(string? memberRole)
    {
        // Arrange
        ArrangeMembership(memberRole, memberActive: false);

        // Act
        var result = await _sut.SubmitPicksAsync(QuinielaId, RoundId, UserId, Picks((1, 11)));

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("No eres miembro activo");
        _events.Verify(r => r.UpsertPicksAsync(It.IsAny<IEnumerable<EventPick>>()), Times.Never);
    }

    [Fact]
    public async Task SubmitPicksAsync_RoundNotFound_ReturnsFailure()
    {
        // Arrange
        ArrangeMembership("MEMBER");
        ArrangeRoundWithDetails(null);

        // Act
        var result = await _sut.SubmitPicksAsync(QuinielaId, RoundId, UserId, Picks((1, 11)));

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Ronda no encontrada");
    }

    [Fact]
    public async Task SubmitPicksAsync_RoundBelongsToAnotherQuiniela_ReturnsFailure()
    {
        // Arrange
        ArrangeMembership("MEMBER");
        ArrangeRoundWithDetails(BuildRound(quinielaId: 999));

        // Act
        var result = await _sut.SubmitPicksAsync(QuinielaId, RoundId, UserId, Picks((1, 11)));

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Ronda no encontrada");
    }

    [Theory]
    [InlineData("Locked")]
    [InlineData("Resolved")]
    public async Task SubmitPicksAsync_RoundLockedByStatus_IsRejected(string status)
    {
        // Arrange
        var (_, q1, _) = ArrangeSubmitScenario(status: status, lockDateUtc: DateTime.UtcNow.AddDays(1));

        // Act
        var result = await _sut.SubmitPicksAsync(QuinielaId, RoundId, UserId, Picks((q1.Id, q1.Options.ElementAt(0).Id)));

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("bloqueada");
        _events.Verify(r => r.UpsertPicksAsync(It.IsAny<IEnumerable<EventPick>>()), Times.Never);
    }

    [Fact]
    public async Task SubmitPicksAsync_LockDateAlreadyPassed_IsRejectedEvenIfStatusOpen()
    {
        // Arrange
        var (_, q1, _) = ArrangeSubmitScenario(status: "Open", lockDateUtc: DateTime.UtcNow.AddSeconds(-1));

        // Act
        var result = await _sut.SubmitPicksAsync(QuinielaId, RoundId, UserId, Picks((q1.Id, q1.Options.ElementAt(0).Id)));

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("bloqueada");
        _events.Verify(r => r.UpsertPicksAsync(It.IsAny<IEnumerable<EventPick>>()), Times.Never);
    }

    [Fact]
    public async Task SubmitPicksAsync_EmptyPicks_IsRejected()
    {
        // Arrange
        ArrangeSubmitScenario();

        // Act
        var result = await _sut.SubmitPicksAsync(QuinielaId, RoundId, UserId, new SubmitEventPicksDto());

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("al menos un pronóstico");
    }

    [Fact]
    public async Task SubmitPicksAsync_NullPicks_IsRejected()
    {
        // Arrange
        ArrangeSubmitScenario();

        // Act
        var result = await _sut.SubmitPicksAsync(QuinielaId, RoundId, UserId, new SubmitEventPicksDto { Picks = null! });

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("al menos un pronóstico");
    }

    [Fact]
    public async Task SubmitPicksAsync_QuestionNotInRound_IsRejected()
    {
        // Arrange
        ArrangeSubmitScenario();

        // Act
        var result = await _sut.SubmitPicksAsync(QuinielaId, RoundId, UserId, Picks((999, 11)));

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("999").And.Contain("no pertenece a esta ronda");
        _events.Verify(r => r.UpsertPicksAsync(It.IsAny<IEnumerable<EventPick>>()), Times.Never);
    }

    [Fact]
    public async Task SubmitPicksAsync_InactiveQuestion_IsRejected()
    {
        // Arrange
        var (round, _, _) = ArrangeSubmitScenario();
        var inactive = AddQuestion(round, 3, active: false);

        // Act
        var result = await _sut.SubmitPicksAsync(QuinielaId, RoundId, UserId, Picks((inactive.Id, inactive.Options.First().Id)));

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("no pertenece a esta ronda");
    }

    [Fact]
    public async Task SubmitPicksAsync_OptionDoesNotExist_IsRejected()
    {
        // Arrange
        var (_, q1, _) = ArrangeSubmitScenario();

        // Act
        var result = await _sut.SubmitPicksAsync(QuinielaId, RoundId, UserId, Picks((q1.Id, 9999)));

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("9999").And.Contain("no es válida");
        _events.Verify(r => r.UpsertPicksAsync(It.IsAny<IEnumerable<EventPick>>()), Times.Never);
    }

    [Fact]
    public async Task SubmitPicksAsync_OptionBelongsToAnotherQuestion_IsRejected()
    {
        // Arrange
        var (_, q1, q2) = ArrangeSubmitScenario();

        // Act
        var result = await _sut.SubmitPicksAsync(QuinielaId, RoundId, UserId, Picks((q1.Id, q2.Options.ElementAt(0).Id)));

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("no es válida");
    }

    [Fact]
    public async Task SubmitPicksAsync_InactiveOption_IsRejected()
    {
        // Arrange
        var (_, q1, _) = ArrangeSubmitScenario();
        q1.Options.ElementAt(0).Active = false;

        // Act
        var result = await _sut.SubmitPicksAsync(QuinielaId, RoundId, UserId, Picks((q1.Id, q1.Options.ElementAt(0).Id)));

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("no es válida");
    }

    [Fact]
    public async Task SubmitPicksAsync_OneInvalidPickAmongValid_RejectsWholeBatch()
    {
        // Arrange
        var (_, q1, q2) = ArrangeSubmitScenario();

        // Act
        var result = await _sut.SubmitPicksAsync(QuinielaId, RoundId, UserId,
            Picks((q1.Id, q1.Options.ElementAt(0).Id), (q2.Id, 9999)));

        // Assert
        result.isSuccess.Should().BeFalse();
        _events.Verify(r => r.UpsertPicksAsync(It.IsAny<IEnumerable<EventPick>>()), Times.Never);
    }

    [Fact]
    public async Task SubmitPicksAsync_ValidPicks_UpsertsPicksForMemberAndReturnsRoundDetail()
    {
        // Arrange
        var (_, q1, q2) = ArrangeSubmitScenario();
        List<EventPick>? saved = null;
        _events.Setup(r => r.UpsertPicksAsync(It.IsAny<IEnumerable<EventPick>>()))
            .Callback<IEnumerable<EventPick>>(p => saved = p.ToList())
            .ReturnsAsync(true);

        // Act
        var result = await _sut.SubmitPicksAsync(QuinielaId, RoundId, UserId,
            Picks((q1.Id, q1.Options.ElementAt(0).Id), (q2.Id, q2.Options.ElementAt(1).Id)));

        // Assert
        result.isSuccess.Should().BeTrue();
        result.Message.Should().Contain("Pronósticos guardados");
        result.Data.Should().NotBeNull();
        result.Data.Id.Should().Be(RoundId);
        saved.Should().NotBeNull();
        saved!.Should().HaveCount(2);
        saved.Should().OnlyContain(p => p.QuinielaMemberId == MemberId && p.Active);
        saved.Select(p => (p.EventQuestionId, p.EventOptionId))
            .Should().BeEquivalentTo(new[] { (q1.Id, q1.Options.ElementAt(0).Id), (q2.Id, q2.Options.ElementAt(1).Id) });
    }

    [Fact]
    public async Task SubmitPicksAsync_UpsertFails_ReturnsFailure()
    {
        // Arrange
        var (_, q1, _) = ArrangeSubmitScenario();
        _events.Setup(r => r.UpsertPicksAsync(It.IsAny<IEnumerable<EventPick>>())).ReturnsAsync(false);

        // Act
        var result = await _sut.SubmitPicksAsync(QuinielaId, RoundId, UserId, Picks((q1.Id, q1.Options.ElementAt(0).Id)));

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Error al registrar los pronósticos");
    }

    #endregion

    #region ResolveRoundAsync

    private static ResolveEventRoundDto Resolutions(params (int question, int winningOption)[] items)
    {
        return new ResolveEventRoundDto
        {
            Resolutions = items.Select(i => new QuestionResolutionItemDto
            {
                EventQuestionId = i.question,
                WinningOptionId = i.winningOption
            }).ToList()
        };
    }

    [Theory]
    [InlineData("MEMBER")]
    [InlineData(null)]
    public async Task ResolveRoundAsync_NotOwnerNorAdmin_IsRejected(string? memberRole)
    {
        // Arrange
        ArrangeMembership(memberRole);

        // Act
        var result = await _sut.ResolveRoundAsync(QuinielaId, RoundId, UserId, Resolutions((1, 11)));

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Solo el creador o administrador");
        _events.Verify(r => r.UpdateRoundAsync(It.IsAny<EventRound>()), Times.Never);
        _uow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveRoundAsync_EmptyResolutions_IsRejected()
    {
        // Arrange
        ArrangeMembership("OWNER");

        // Act
        var result = await _sut.ResolveRoundAsync(QuinielaId, RoundId, UserId, new ResolveEventRoundDto());

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("resultados de las preguntas");
    }

    [Fact]
    public async Task ResolveRoundAsync_RoundNotFound_ReturnsFailureWithoutTouchingMembers()
    {
        // Arrange
        ArrangeMembership("OWNER");
        ArrangeRoundWithDetails(null);

        // Act
        var result = await _sut.ResolveRoundAsync(QuinielaId, RoundId, UserId, Resolutions((1, 11)));

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Ronda no encontrada");
        _members.Verify(r => r.UpdateAsync(It.IsAny<QuinielaMember>()), Times.Never);
        _uow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveRoundAsync_RoundBelongsToAnotherQuiniela_ReturnsFailure()
    {
        // Arrange
        ArrangeMembership("OWNER");
        ArrangeRoundWithDetails(BuildRound(quinielaId: 999));

        // Act
        var result = await _sut.ResolveRoundAsync(QuinielaId, RoundId, UserId, Resolutions((1, 11)));

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Ronda no encontrada");
    }

    [Fact]
    public async Task ResolveRoundAsync_ValidResolution_EvaluatesPicksAndMarksOptionsAndRound()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("OWNER");
        var round = BuildRound(status: "Locked");
        var q1 = AddQuestion(round, 1, points: 3); // opciones 11, 12
        var q2 = AddQuestion(round, 2, points: 2); // opciones 21, 22
        var p1 = AddPick(q1, memberId: 1, optionId: 11, submittedAtUtc: DateTime.UtcNow.AddHours(-3));
        var p2 = AddPick(q1, memberId: 2, optionId: 12, submittedAtUtc: DateTime.UtcNow.AddHours(-3));
        var p3 = AddPick(q2, memberId: 1, optionId: 22, submittedAtUtc: DateTime.UtcNow.AddHours(-2));
        var roundPicks = new[] { p1, p2, p3 };
        ArrangeResolveRepos(round, roundPicks, roundPicks,
            new[] { new QuinielaMember { Id = 1, Alias = "Ana" }, new QuinielaMember { Id = 2, Alias = "Beto" } });

        // Act
        var result = await _sut.ResolveRoundAsync(QuinielaId, RoundId, UserId, Resolutions((1, 11), (2, 21)));

        // Assert
        result.isSuccess.Should().BeTrue();
        result.Message.Should().Contain("Ronda resuelta");
        result.Data.Status.Should().Be("Resolved");
        p1.IsHit.Should().BeTrue();
        p1.PointsEarned.Should().Be(3);
        p2.IsHit.Should().BeFalse();
        p2.PointsEarned.Should().Be(0);
        p3.IsHit.Should().BeFalse();
        p3.PointsEarned.Should().Be(0);
        q1.Options.Single(o => o.Id == 11).IsCorrect.Should().BeTrue();
        q1.Options.Single(o => o.Id == 12).IsCorrect.Should().BeFalse();
        q2.Options.Single(o => o.Id == 21).IsCorrect.Should().BeTrue();
        q2.Options.Single(o => o.Id == 22).IsCorrect.Should().BeFalse();
        round.Status.Should().Be("Resolved");
        round.ResolvedDateUtc.Should().NotBeNull();
        round.LastModifiedBy.Should().Be("tester");
        _events.Verify(r => r.UpdateRoundAsync(round), Times.Once);
        _events.Verify(r => r.UpsertPicksAsync(It.Is<IEnumerable<EventPick>>(l => l.Count() == 3)), Times.Once);
        _uow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveRoundAsync_AssignsTotalPointsAndTotalHitsToMembersIncludingSportsPicks()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("OWNER");
        var round = BuildRound(status: "Locked");
        var q1 = AddQuestion(round, 1, points: 3); // opciones 11, 12
        var q2 = AddQuestion(round, 2, points: 2); // opciones 21, 22

        // m1 acierta ambas (5 pts, 2 hits); m2 falla q1 y acierta q2 (2 pts, 1 hit)
        var m1q1 = AddPick(q1, memberId: 1, optionId: 11, submittedAtUtc: DateTime.UtcNow.AddHours(-3));
        var m1q2 = AddPick(q2, memberId: 1, optionId: 21, submittedAtUtc: DateTime.UtcNow.AddHours(-2));
        var m2q1 = AddPick(q1, memberId: 2, optionId: 12, submittedAtUtc: DateTime.UtcNow.AddHours(-3));
        var m2q2 = AddPick(q2, memberId: 2, optionId: 21, submittedAtUtc: DateTime.UtcNow.AddHours(-2));
        var roundPicks = new[] { m1q1, m1q2, m2q1, m2q2 };

        // Pick previo de m1 en otra ronda ya resuelta (acumula) y uno sin resolver de m2 (se ignora)
        var previousHit = new EventPick
        {
            Id = 5000, EventQuestionId = 900, EventOptionId = 1, QuinielaMemberId = 1,
            IsHit = true, PointsEarned = 4, SubmittedAtUtc = DateTime.UtcNow.AddDays(-5), Active = true
        };
        var unresolved = new EventPick
        {
            Id = 5001, EventQuestionId = 901, EventOptionId = 1, QuinielaMemberId = 2,
            IsHit = null, PointsEarned = 99, SubmittedAtUtc = DateTime.UtcNow.AddDays(-1), Active = true
        };
        var allEventPicks = roundPicks.Concat(new[] { previousHit, unresolved }).ToArray();

        var member1 = new QuinielaMember { Id = 1, Alias = "Ana", BestStreak = 0 };
        var member2 = new QuinielaMember { Id = 2, Alias = "Beto", BestStreak = 5 };
        ArrangeResolveRepos(round, roundPicks, allEventPicks, new[] { member1, member2 },
            sportsPicks: new[]
            {
                new Pick { Id = 1, MemberId = 2, IsHit = true },
                new Pick { Id = 2, MemberId = 2, IsHit = true },
                new Pick { Id = 3, MemberId = 2, IsHit = false },
                new Pick { Id = 4, MemberId = 2, IsHit = null },
                new Pick { Id = 5, MemberId = 999, IsHit = true } // miembro ajeno
            });

        // Act
        var result = await _sut.ResolveRoundAsync(QuinielaId, RoundId, UserId, Resolutions((1, 11), (2, 21)));

        // Assert
        result.isSuccess.Should().BeTrue();

        member1.TotalHits.Should().Be(3);          // 1 previo + 2 de la ronda
        member1.TotalPoints.Should().Be(9);        // 4 + 3 + 2
        member1.CurrentStreak.Should().Be(3);
        member1.BestStreak.Should().Be(3);
        member1.LastModified.Should().NotBeNull();

        member2.TotalHits.Should().Be(3);          // 1 evento + 2 deportivos acertados
        member2.TotalPoints.Should().Be(4);        // 2 de evento + 2 (1 por cada acierto deportivo)
        member2.CurrentStreak.Should().Be(1);
        member2.BestStreak.Should().Be(5);         // conserva su mejor racha histórica

        _members.Verify(r => r.UpdateAsync(member1), Times.Once);
        _members.Verify(r => r.UpdateAsync(member2), Times.Once);
        _members.Verify(r => r.UpdateAsync(It.IsAny<QuinielaMember>()), Times.Exactly(2));
        _uow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveRoundAsync_MemberWithoutPicks_GetsZeroTotals()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("ADMIN");
        var round = BuildRound(status: "Locked");
        var q1 = AddQuestion(round, 1, points: 3);
        var hit = AddPick(q1, memberId: 1, optionId: 11);
        var silent = new QuinielaMember { Id = 3, Alias = "Silencioso", TotalPoints = 50, TotalHits = 50 };
        ArrangeResolveRepos(round, new[] { hit }, new[] { hit },
            new[] { new QuinielaMember { Id = 1, Alias = "Ana" }, silent });

        // Act
        var result = await _sut.ResolveRoundAsync(QuinielaId, RoundId, UserId, Resolutions((1, 11)));

        // Assert
        result.isSuccess.Should().BeTrue();
        silent.TotalPoints.Should().Be(0);
        silent.TotalHits.Should().Be(0);
        silent.CurrentStreak.Should().Be(0);
    }

    [Fact]
    public async Task ResolveRoundAsync_QuestionNotInResolutions_LeavesItsPicksUnevaluated()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership("OWNER");
        var round = BuildRound(status: "Locked");
        var q1 = AddQuestion(round, 1, points: 3);
        var q2 = AddQuestion(round, 2, points: 2);
        var resolved = AddPick(q1, memberId: 1, optionId: 11);
        var pending = AddPick(q2, memberId: 1, optionId: 21);
        var member = new QuinielaMember { Id = 1, Alias = "Ana" };
        ArrangeResolveRepos(round, new[] { resolved, pending }, new[] { resolved, pending }, new[] { member });

        // Act
        var result = await _sut.ResolveRoundAsync(QuinielaId, RoundId, UserId, Resolutions((1, 11)));

        // Assert
        result.isSuccess.Should().BeTrue();
        resolved.IsHit.Should().BeTrue();
        pending.IsHit.Should().BeNull();
        pending.PointsEarned.Should().Be(0);
        member.TotalHits.Should().Be(1);
        member.TotalPoints.Should().Be(3);
    }

    [Fact]
    public async Task ResolveRoundAsync_GlobalAdminWithoutMembership_CanResolve()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeMembership(memberRole: null, userRole: "ADMIN");
        var round = BuildRound(status: "Locked");
        var q1 = AddQuestion(round, 1, points: 1);
        var pick = AddPick(q1, memberId: 1, optionId: 11);
        ArrangeResolveRepos(round, new[] { pick }, new[] { pick }, new[] { new QuinielaMember { Id = 1, Alias = "Ana" } });

        // Act
        var result = await _sut.ResolveRoundAsync(QuinielaId, RoundId, UserId, Resolutions((1, 11)));

        // Assert
        result.isSuccess.Should().BeTrue();
        pick.IsHit.Should().BeTrue();
    }

    private void ArrangeResolveRepos(
        EventRound round,
        IEnumerable<EventPick> roundPicks,
        IEnumerable<EventPick> allQuinielaPicks,
        IEnumerable<QuinielaMember> members,
        IEnumerable<Pick>? sportsPicks = null)
    {
        ArrangeRoundWithDetails(round);
        _events.Setup(r => r.GetPicksByRoundIdAsync(RoundId)).ReturnsAsync(roundPicks.ToList());
        _events.Setup(r => r.UpdateRoundAsync(It.IsAny<EventRound>())).ReturnsAsync(true);
        _events.Setup(r => r.UpsertPicksAsync(It.IsAny<IEnumerable<EventPick>>())).ReturnsAsync(true);
        _events.Setup(r => r.GetAllEventPicksForQuinielaAsync(QuinielaId)).ReturnsAsync(allQuinielaPicks.ToList());
        _members.Setup(r => r.GetMembersAsync(QuinielaId)).ReturnsAsync(members.ToList());
        _members.Setup(r => r.UpdateAsync(It.IsAny<QuinielaMember>())).ReturnsAsync(true);
        _sportsPicks.Setup(r => r.GetAllPicksForQuinielaAsync(QuinielaId))
            .ReturnsAsync((sportsPicks ?? Array.Empty<Pick>()).ToList());
    }

    #endregion

    #region GetStandingsAsync

    private void ArrangeStandingsRepos(
        IEnumerable<QuinielaMember> members,
        IEnumerable<EventPick>? allPicks = null,
        IEnumerable<EventRound>? allRounds = null)
    {
        _members.Setup(r => r.GetMembersAsync(QuinielaId)).ReturnsAsync(members.ToList());
        _events.Setup(r => r.GetAllEventPicksForQuinielaAsync(QuinielaId))
            .ReturnsAsync((allPicks ?? Array.Empty<EventPick>()).ToList());
        _events.Setup(r => r.GetRoundsByQuinielaIdAsync(QuinielaId))
            .ReturnsAsync((allRounds ?? Array.Empty<EventRound>()).ToList());
    }

    [Fact]
    public async Task GetStandingsAsync_QuinielaDoesNotExist_ReturnsFailure()
    {
        // Arrange
        ArrangeMissingQuiniela();

        // Act
        var result = await _sut.GetStandingsAsync(QuinielaId, null);

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("no existe o está inactiva");
    }

    [Fact]
    public async Task GetStandingsAsync_QuinielaInactive_ReturnsFailure()
    {
        // Arrange
        ArrangeActiveQuiniela(isActive: false);

        // Act
        var result = await _sut.GetStandingsAsync(QuinielaId, null);

        // Assert
        result.isSuccess.Should().BeFalse();
        _members.Verify(r => r.GetMembersAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetStandingsAsync_WithoutRoundId_ReturnsOnlyGeneralStandingsOrderedByPoints()
    {
        // Arrange
        ArrangeActiveQuiniela();
        var round = BuildRound(status: "Resolved");
        var q1 = AddQuestion(round, 1, points: 3);
        var q2 = AddQuestion(round, 2, points: 2);
        AddQuestion(round, 3, points: 1, active: false); // no cuenta
        var anaHit = AddPick(q1, 1, q1.Options.ElementAt(0).Id, isHit: true, pointsEarned: 3);
        var betoHit = AddPick(q2, 2, q2.Options.ElementAt(0).Id, isHit: true, pointsEarned: 2);
        var betoHit2 = AddPick(q1, 2, q1.Options.ElementAt(0).Id, isHit: true, pointsEarned: 3);
        var anaMiss = AddPick(q2, 1, q2.Options.ElementAt(1).Id, isHit: false);
        var unresolved = AddPick(q2, 3, q2.Options.ElementAt(0).Id, isHit: null);
        ArrangeStandingsRepos(
            new[]
            {
                new QuinielaMember { Id = 1, Alias = "Ana" },
                new QuinielaMember { Id = 2, Alias = "Beto" },
                new QuinielaMember { Id = 3, Alias = "Carla" }
            },
            allPicks: new[] { anaHit, betoHit, betoHit2, anaMiss, unresolved },
            allRounds: new[] { round });

        // Act
        var result = await _sut.GetStandingsAsync(QuinielaId, null);

        // Assert
        result.isSuccess.Should().BeTrue();
        result.Data.QuinielaId.Should().Be(QuinielaId);
        result.Data.RoundId.Should().BeNull();
        result.Data.RoundTitle.Should().BeNull();
        result.Data.RoundStatus.Should().BeNull();
        result.Data.RoundStandings.Should().BeEmpty();
        result.Data.GeneralStandings.Select(s => s.Alias).Should().ContainInOrder("Beto", "Ana", "Carla");
        result.Data.GeneralStandings.Select(s => s.Rank).Should().ContainInOrder(1, 2, 3);
        var beto = result.Data.GeneralStandings[0];
        beto.Points.Should().Be(5);
        beto.Hits.Should().Be(2);
        beto.TotalQuestions.Should().Be(2); // solo preguntas activas de rondas resueltas
        var carla = result.Data.GeneralStandings[2];
        carla.Points.Should().Be(0);
        carla.Hits.Should().Be(0);
        _events.Verify(r => r.GetRoundByIdWithDetailsAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetStandingsAsync_TotalQuestionsIgnoresRoundsNotResolved()
    {
        // Arrange
        ArrangeActiveQuiniela();
        var resolved = BuildRound(status: "Resolved");
        AddQuestion(resolved, 1);
        var open = BuildRound(status: "Open");
        open.Id = 101;
        AddQuestion(open, 2);
        AddQuestion(open, 3);
        ArrangeStandingsRepos(new[] { new QuinielaMember { Id = 1, Alias = "Ana" } }, allRounds: new[] { resolved, open });

        // Act
        var result = await _sut.GetStandingsAsync(QuinielaId, null);

        // Assert
        result.Data.GeneralStandings.Should().ContainSingle().Which.TotalQuestions.Should().Be(1);
    }

    [Fact]
    public async Task GetStandingsAsync_WithRoundId_IncludesRoundTitleStatusAndRoundStandings()
    {
        // Arrange
        ArrangeActiveQuiniela();
        var round = BuildRound(status: "Resolved");
        var q1 = AddQuestion(round, 1, points: 4);
        var q2 = AddQuestion(round, 2, points: 1);
        var anaQ1 = AddPick(q1, 1, q1.Options.ElementAt(0).Id, isHit: true, pointsEarned: 4);
        var anaQ2 = AddPick(q2, 1, q2.Options.ElementAt(0).Id, isHit: true, pointsEarned: 1);
        var betoQ1 = AddPick(q1, 2, q1.Options.ElementAt(1).Id, isHit: false);
        ArrangeRoundWithDetails(round);
        _events.Setup(r => r.GetPicksByRoundIdAsync(RoundId)).ReturnsAsync(new[] { anaQ1, anaQ2, betoQ1 });
        ArrangeStandingsRepos(
            new[] { new QuinielaMember { Id = 2, Alias = "Beto" }, new QuinielaMember { Id = 1, Alias = "Ana" } },
            allPicks: new[] { anaQ1, anaQ2, betoQ1 },
            allRounds: new[] { round });

        // Act
        var result = await _sut.GetStandingsAsync(QuinielaId, RoundId);

        // Assert
        result.isSuccess.Should().BeTrue();
        result.Data.RoundId.Should().Be(RoundId);
        result.Data.RoundTitle.Should().Be("Ronda 1");
        result.Data.RoundStatus.Should().Be("Resolved");
        result.Data.RoundStandings.Should().HaveCount(2);
        result.Data.RoundStandings[0].Alias.Should().Be("Ana");
        result.Data.RoundStandings[0].Rank.Should().Be(1);
        result.Data.RoundStandings[0].Points.Should().Be(5);
        result.Data.RoundStandings[0].Hits.Should().Be(2);
        result.Data.RoundStandings[0].TotalQuestions.Should().Be(2);
        result.Data.RoundStandings[1].Alias.Should().Be("Beto");
        result.Data.RoundStandings[1].Points.Should().Be(0);
        result.Data.GeneralStandings.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task GetStandingsAsync_NonPositiveRoundId_SkipsRoundStandings(int roundId)
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeStandingsRepos(new[] { new QuinielaMember { Id = 1, Alias = "Ana" } });

        // Act
        var result = await _sut.GetStandingsAsync(QuinielaId, roundId);

        // Assert
        result.isSuccess.Should().BeTrue();
        result.Data.RoundStandings.Should().BeEmpty();
        result.Data.RoundTitle.Should().BeNull();
        _events.Verify(r => r.GetRoundByIdWithDetailsAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetStandingsAsync_RoundNotFoundOrInactive_ReturnsGeneralStandingsOnly()
    {
        // Arrange
        ArrangeActiveQuiniela();
        ArrangeRoundWithDetails(BuildRound(active: false));
        ArrangeStandingsRepos(new[] { new QuinielaMember { Id = 1, Alias = "Ana" } });

        // Act
        var result = await _sut.GetStandingsAsync(QuinielaId, RoundId);

        // Assert
        result.isSuccess.Should().BeTrue();
        result.Data.RoundId.Should().Be(RoundId);
        result.Data.RoundTitle.Should().BeNull();
        result.Data.RoundStatus.Should().BeNull();
        result.Data.RoundStandings.Should().BeEmpty();
        result.Data.GeneralStandings.Should().ContainSingle();
        _events.Verify(r => r.GetPicksByRoundIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetStandingsAsync_TiedPoints_BreaksTieByHitsThenAlias()
    {
        // Arrange
        ArrangeActiveQuiniela();
        var round = BuildRound(status: "Resolved");
        var q1 = AddQuestion(round, 1, points: 2);
        var q2 = AddQuestion(round, 2, points: 1);
        var q3 = AddQuestion(round, 3, points: 1);
        // Zoe: 2 pts / 1 hit. Beto: 2 pts / 2 hits. Ana: 2 pts / 1 hit -> orden: Beto, Ana, Zoe
        var zoe = AddPick(q1, 3, q1.Options.ElementAt(0).Id, isHit: true, pointsEarned: 2);
        var anaPick = AddPick(q1, 1, q1.Options.ElementAt(0).Id, isHit: true, pointsEarned: 2);
        var beto1 = AddPick(q2, 2, q2.Options.ElementAt(0).Id, isHit: true, pointsEarned: 1);
        var beto2 = AddPick(q3, 2, q3.Options.ElementAt(0).Id, isHit: true, pointsEarned: 1);
        ArrangeStandingsRepos(
            new[]
            {
                new QuinielaMember { Id = 3, Alias = "Zoe" },
                new QuinielaMember { Id = 1, Alias = "Ana" },
                new QuinielaMember { Id = 2, Alias = "Beto" }
            },
            allPicks: new[] { zoe, anaPick, beto1, beto2 },
            allRounds: new[] { round });

        // Act
        var result = await _sut.GetStandingsAsync(QuinielaId, null);

        // Assert
        result.Data.GeneralStandings.Select(s => s.Alias).Should().ContainInOrder("Beto", "Ana", "Zoe");
    }

    #endregion
}
