using Domain.Entities;
using DTO.Challenges;
using DTO.Notifications;
using FluentAssertions;
using Interface.Persistence;
using Interface.UseCases;
using Moq;
using UseCases.Challenges;
using MatchEntity = Domain.Entities.Match;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class ChallengeApplicationTests
{
    private const int QuinielaId = 1;
    private const int WeekId = 5;
    private const int ChallengeId = 77;
    private const int TargetMatchId = 100;

    private const int CreatorMemberId = 1;
    private const int CreatorUserId = 10;
    private const int RivalMemberId = 2;
    private const int RivalUserId = 20;
    private const int ThirdMemberId = 3;
    private const int ThirdUserId = 30;
    private const int FourthMemberId = 4;
    private const int FourthUserId = 40;

    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IChallengeRepository> _challengeRepoMock;
    private readonly Mock<IWeekRepository> _weekRepoMock;
    private readonly Mock<IMatchRepository> _matchRepoMock;
    private readonly Mock<IQuinielaRepository> _quinielaRepoMock;
    private readonly Mock<IQuinielaMemberRepository> _memberRepoMock;
    private readonly Mock<IPickRepository> _pickRepoMock;
    private readonly Mock<IWeeklyAwardRepository> _awardRepoMock;
    private readonly Mock<IWebPushNotificationService> _webPushMock;
    private readonly ChallengeApplication _challengeApplication;

    private readonly List<QuinielaMember> _members;

    public ChallengeApplicationTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _challengeRepoMock = new Mock<IChallengeRepository>();
        _weekRepoMock = new Mock<IWeekRepository>();
        _matchRepoMock = new Mock<IMatchRepository>();
        _quinielaRepoMock = new Mock<IQuinielaRepository>();
        _memberRepoMock = new Mock<IQuinielaMemberRepository>();
        _pickRepoMock = new Mock<IPickRepository>();
        _awardRepoMock = new Mock<IWeeklyAwardRepository>();
        _webPushMock = new Mock<IWebPushNotificationService>();

        _unitOfWorkMock.Setup(u => u.Challenges).Returns(_challengeRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Weeks).Returns(_weekRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Matches).Returns(_matchRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Quinielas).Returns(_quinielaRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.QuinielaMembers).Returns(_memberRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Picks).Returns(_pickRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.WeeklyAwards).Returns(_awardRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Save(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _members = new List<QuinielaMember>
        {
            new() { Id = CreatorMemberId, QuinielaId = QuinielaId, UserId = CreatorUserId, Alias = "ElRetador", Role = "MEMBER" },
            new() { Id = RivalMemberId, QuinielaId = QuinielaId, UserId = RivalUserId, Alias = "ElRival", Role = "MEMBER" },
            new() { Id = ThirdMemberId, QuinielaId = QuinielaId, UserId = ThirdUserId, Alias = "ElTercero", Role = "MEMBER" },
            new() { Id = FourthMemberId, QuinielaId = QuinielaId, UserId = FourthUserId, Alias = "ElCuarto", Role = "ADMIN" }
        };

        foreach (var member in _members)
        {
            _memberRepoMock.Setup(r => r.GetMembershipAsync(QuinielaId, member.UserId)).ReturnsAsync(member);
        }

        _memberRepoMock.Setup(r => r.GetMembersAsync(QuinielaId)).ReturnsAsync(_members);
        _quinielaRepoMock.Setup(r => r.GetAsync(QuinielaId))
            .ReturnsAsync(new Quiniela { Id = QuinielaId, Name = "Quiniela de la Oficina", Active = true });
        _pickRepoMock.Setup(r => r.GetAllPicksForWeekAsync(QuinielaId, It.IsAny<int>())).ReturnsAsync(new List<Pick>());
        _pickRepoMock.Setup(r => r.GetAllPicksForQuinielaAsync(QuinielaId)).ReturnsAsync(new List<Pick>());
        _awardRepoMock.Setup(r => r.GetAllAwardsForQuinielaAsync(QuinielaId)).ReturnsAsync(new List<WeeklyAward>());
        _challengeRepoMock.Setup(r => r.GetActiveOrPendingChallengesByWeekAsync(QuinielaId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QuinielaChallenge>());
        _challengeRepoMock.Setup(r => r.GetChallengesByWeekAsync(QuinielaId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QuinielaChallenge>());

        _challengeApplication = new ChallengeApplication(
            _unitOfWorkMock.Object,
            webPushService: _webPushMock.Object);
    }

    #region Helpers

    private static Week BuildOpenWeek() => new()
    {
        Id = WeekId,
        SeasonId = 1,
        WeekNumber = 5,
        Name = "Jornada 5",
        Status = "PUBLISHED",
        FirstGameUtc = DateTime.UtcNow.AddDays(1)
    };

    private static MatchEntity BuildMatch(string statusState = "pre", string? winnerAbbr = null) => new()
    {
        Id = TargetMatchId,
        WeekId = WeekId,
        DateUtc = DateTime.UtcNow.AddDays(1),
        StatusState = statusState,
        WinnerAbbr = winnerAbbr,
        HomeTeam = new Team { Id = 1, Abbreviation = "AME", Name = "América" },
        AwayTeam = new Team { Id = 2, Abbreviation = "GDL", Name = "Guadalajara" }
    };

    private void SetupWeek(Week week, params MatchEntity[] matches)
    {
        _weekRepoMock.Setup(r => r.GetAsync(week.Id)).ReturnsAsync(week);
        _matchRepoMock.Setup(r => r.GetByWeekIdAsync(week.Id)).ReturnsAsync(matches.ToList());
    }

    private static QuinielaChallenge BuildChallenge(
        string challengeType,
        string status,
        Week week,
        params QuinielaChallengeParticipant[] participants) => new()
    {
        Id = ChallengeId,
        QuinielaId = QuinielaId,
        WeekId = week.Id,
        Week = week,
        CreatorMemberId = CreatorMemberId,
        ChallengeType = challengeType,
        PayoutMode = "WINNER_TAKES_ALL",
        StakesDescription = "Unos tacos al pastor",
        TargetMatchId = challengeType is "MATCH_WINNER" or "EXACT_SCORE" ? TargetMatchId : null,
        Status = status,
        Participants = participants.ToList()
    };

    private static QuinielaChallengeParticipant Participant(
        int memberId,
        string status,
        string outcome = "NONE") => new()
    {
        Id = memberId * 10,
        ChallengeId = ChallengeId,
        MemberId = memberId,
        Status = status,
        Outcome = outcome
    };

    private void SetupChallenge(QuinielaChallenge challenge)
    {
        _challengeRepoMock.Setup(r => r.GetByIdWithParticipantsAsync(challenge.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(challenge);
    }

    #endregion

    #region CreateChallengeAsync

    [Fact]
    public async Task CreateChallengeAsync_ValidMatchWinner_InsertsChallengeAndSendsWebPushToInvited()
    {
        // Arrange
        var week = BuildOpenWeek();
        SetupWeek(week, BuildMatch());

        QuinielaChallenge? inserted = null;
        _challengeRepoMock.Setup(r => r.InsertAsync(It.IsAny<QuinielaChallenge>()))
            .Callback<QuinielaChallenge>(c => inserted = c)
            .ReturnsAsync(true);

        var dto = new CreateChallengeDto
        {
            ChallengeType = "match_winner",
            StakesDescription = "  Unos tacos al pastor  ",
            InvitedMemberIds = new List<int> { RivalMemberId, ThirdMemberId },
            TargetMatchId = TargetMatchId,
            CreatorPredictedWinnerAbbr = "ame"
        };

        // Act
        var result = await _challengeApplication.CreateChallengeAsync(QuinielaId, WeekId, CreatorUserId, dto);

        // Assert
        inserted.Should().NotBeNull();
        inserted!.Status.Should().Be("PENDING_ACCEPTANCE");
        inserted.ChallengeType.Should().Be("MATCH_WINNER");
        inserted.PayoutMode.Should().Be("WINNER_TAKES_ALL");
        inserted.StakesDescription.Should().Be("Unos tacos al pastor");
        inserted.TargetMatchId.Should().Be(TargetMatchId);
        inserted.Participants.Should().HaveCount(3);

        var creatorParticipant = inserted.Participants.Single(p => p.MemberId == CreatorMemberId);
        creatorParticipant.Status.Should().Be("ACCEPTED");
        creatorParticipant.PredictedWinnerAbbr.Should().Be("AME");
        inserted.Participants.Where(p => p.MemberId != CreatorMemberId)
            .Should().OnlyContain(p => p.Status == "PENDING");

        result.Status.Should().Be("PENDING_ACCEPTANCE");
        result.WeekName.Should().Be("Jornada 5");
        result.CreatorAlias.Should().Be("ElRetador");
        result.Participants.Should().HaveCount(3);
        result.Participants.First().IsCreator.Should().BeTrue();

        _unitOfWorkMock.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
        _webPushMock.Verify(w => w.SendNotificationToUsersAsync(
                It.Is<IEnumerable<int>>(ids => ids.OrderBy(i => i).SequenceEqual(new[] { RivalUserId, ThirdUserId })),
                It.Is<PushNotificationPayload>(p => p.Title.Contains("Nuevo Reto") && p.Message.Contains("ElRetador")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateChallengeAsync_WebPushThrows_StillReturnsCreatedChallenge()
    {
        // Arrange
        var week = BuildOpenWeek();
        SetupWeek(week, BuildMatch());
        _challengeRepoMock.Setup(r => r.InsertAsync(It.IsAny<QuinielaChallenge>())).ReturnsAsync(true);
        _webPushMock.Setup(w => w.SendNotificationToUsersAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Push caído"));

        var dto = new CreateChallengeDto
        {
            ChallengeType = "TOTAL_GOALS",
            StakesDescription = "Una cena",
            InvitedMemberIds = new List<int> { RivalMemberId },
            CreatorPredictedTotalGoals = 22
        };

        // Act
        var result = await _challengeApplication.CreateChallengeAsync(QuinielaId, WeekId, CreatorUserId, dto);

        // Assert
        result.Status.Should().Be("PENDING_ACCEPTANCE");
        result.ChallengeType.Should().Be("TOTAL_GOALS");
        _unitOfWorkMock.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("LOCKED")]
    [InlineData("DRAFT")]
    [InlineData("SCORED")]
    public async Task CreateChallengeAsync_WeekNotPublished_ThrowsInvalidOperationAndDoesNotInsert(string weekStatus)
    {
        // Arrange
        var week = BuildOpenWeek();
        week.Status = weekStatus;
        SetupWeek(week, BuildMatch());

        var dto = new CreateChallengeDto
        {
            ChallengeType = "WEEKLY_RANK",
            StakesDescription = "Unas chelas",
            InvitedMemberIds = new List<int> { RivalMemberId }
        };

        // Act
        var act = () => _challengeApplication.CreateChallengeAsync(QuinielaId, WeekId, CreatorUserId, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*bloqueada*");
        _challengeRepoMock.Verify(r => r.InsertAsync(It.IsAny<QuinielaChallenge>()), Times.Never);
        _webPushMock.Verify(w => w.SendNotificationToUsersAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateChallengeAsync_FirstGameUtcInThePast_ThrowsInvalidOperationAndDoesNotInsert()
    {
        // Arrange
        var week = BuildOpenWeek();
        week.FirstGameUtc = DateTime.UtcNow.AddMinutes(-5);
        SetupWeek(week, BuildMatch());

        var dto = new CreateChallengeDto
        {
            ChallengeType = "WEEKLY_RANK",
            StakesDescription = "Unas chelas",
            InvitedMemberIds = new List<int> { RivalMemberId }
        };

        // Act
        var act = () => _challengeApplication.CreateChallengeAsync(QuinielaId, WeekId, CreatorUserId, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*bloqueada*");
        _challengeRepoMock.Verify(r => r.InsertAsync(It.IsAny<QuinielaChallenge>()), Times.Never);
        _challengeRepoMock.Verify(r => r.GetActiveOrPendingChallengesByWeekAsync(QuinielaId, WeekId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateChallengeAsync_MatchWinnerWithMoreThanTwoInvited_ThrowsArgumentException()
    {
        // Arrange
        var week = BuildOpenWeek();
        SetupWeek(week, BuildMatch());

        var dto = new CreateChallengeDto
        {
            ChallengeType = "MATCH_WINNER",
            StakesDescription = "Unos tacos",
            InvitedMemberIds = new List<int> { RivalMemberId, ThirdMemberId, FourthMemberId },
            TargetMatchId = TargetMatchId,
            CreatorPredictedWinnerAbbr = "AME"
        };

        // Act
        var act = () => _challengeApplication.CreateChallengeAsync(QuinielaId, WeekId, CreatorUserId, dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*máximo 3 participantes*");
        _challengeRepoMock.Verify(r => r.InsertAsync(It.IsAny<QuinielaChallenge>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region RespondChallengeAsync

    [Fact]
    public async Task RespondChallengeAsync_MatchWinnerDuplicatePick_ThrowsInvalidOperation()
    {
        // Arrange
        var week = BuildOpenWeek();
        SetupWeek(week, BuildMatch());
        var creator = Participant(CreatorMemberId, "ACCEPTED");
        creator.PredictedWinnerAbbr = "AME";
        var challenge = BuildChallenge("MATCH_WINNER", "PENDING_ACCEPTANCE", week, creator, Participant(RivalMemberId, "PENDING"));
        SetupChallenge(challenge);

        var dto = new RespondChallengeDto { Action = "ACCEPT", PredictedWinnerAbbr = " ame " };

        // Act
        var act = () => _challengeApplication.RespondChallengeAsync(QuinielaId, ChallengeId, RivalUserId, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*AME*ya fue elegida*");
        challenge.Participants.Single(p => p.MemberId == RivalMemberId).Status.Should().Be("PENDING");
        _unitOfWorkMock.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RespondChallengeAsync_ExactScoreDuplicateScore_ThrowsInvalidOperation()
    {
        // Arrange
        var week = BuildOpenWeek();
        SetupWeek(week, BuildMatch());
        var creator = Participant(CreatorMemberId, "ACCEPTED");
        creator.PredictedHomeScore = 2;
        creator.PredictedAwayScore = 1;
        var challenge = BuildChallenge("EXACT_SCORE", "PENDING_ACCEPTANCE", week, creator, Participant(RivalMemberId, "PENDING"));
        SetupChallenge(challenge);

        var dto = new RespondChallengeDto { Action = "ACCEPT", PredictedHomeScore = 2, PredictedAwayScore = 1 };

        // Act
        var act = () => _challengeApplication.RespondChallengeAsync(QuinielaId, ChallengeId, RivalUserId, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*2-1*ya fue elegido*");
        _unitOfWorkMock.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RespondChallengeAsync_TotalGoalsDuplicatePrediction_ThrowsInvalidOperation()
    {
        // Arrange
        var week = BuildOpenWeek();
        SetupWeek(week, BuildMatch());
        var creator = Participant(CreatorMemberId, "ACCEPTED");
        creator.PredictedTotalGoals = 25;
        var challenge = BuildChallenge("TOTAL_GOALS", "PENDING_ACCEPTANCE", week, creator, Participant(RivalMemberId, "PENDING"));
        SetupChallenge(challenge);

        var dto = new RespondChallengeDto { Action = "ACCEPT", PredictedTotalGoals = 25 };

        // Act
        var act = () => _challengeApplication.RespondChallengeAsync(QuinielaId, ChallengeId, RivalUserId, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*25 goles ya fue elegida*");
        _unitOfWorkMock.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RespondChallengeAsync_Accept_ActivatesChallengeAndSendsWebPushToCreator()
    {
        // Arrange
        var week = BuildOpenWeek();
        SetupWeek(week, BuildMatch());
        var creator = Participant(CreatorMemberId, "ACCEPTED");
        creator.PredictedWinnerAbbr = "AME";
        var challenge = BuildChallenge("MATCH_WINNER", "PENDING_ACCEPTANCE", week, creator, Participant(RivalMemberId, "PENDING"));
        SetupChallenge(challenge);

        var dto = new RespondChallengeDto { Action = "accept", PredictedWinnerAbbr = "gdl" };

        // Act
        var result = await _challengeApplication.RespondChallengeAsync(QuinielaId, ChallengeId, RivalUserId, dto);

        // Assert
        challenge.Status.Should().Be("ACTIVE");
        var rival = challenge.Participants.Single(p => p.MemberId == RivalMemberId);
        rival.Status.Should().Be("ACCEPTED");
        rival.PredictedWinnerAbbr.Should().Be("GDL");
        rival.RespondedAtUtc.Should().NotBeNull();

        result.Status.Should().Be("ACTIVE");
        result.TakenOptions.Should().BeEquivalentTo(new[] { "AME", "GDL" });

        _challengeRepoMock.Verify(r => r.UpdateAsync(challenge), Times.Once);
        _unitOfWorkMock.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
        _webPushMock.Verify(w => w.SendNotificationToUserAsync(
                CreatorUserId,
                It.Is<PushNotificationPayload>(p => p.Title.Contains("Reto Aceptado") && p.Message.Contains("ElRival")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RespondChallengeAsync_Decline_MarksParticipantDeclinedAndKeepsChallengePending()
    {
        // Arrange
        var week = BuildOpenWeek();
        SetupWeek(week, BuildMatch());
        var challenge = BuildChallenge("WEEKLY_RANK", "PENDING_ACCEPTANCE", week,
            Participant(CreatorMemberId, "ACCEPTED"),
            Participant(RivalMemberId, "PENDING"),
            Participant(ThirdMemberId, "PENDING"));
        SetupChallenge(challenge);

        var dto = new RespondChallengeDto { Action = "DECLINE" };

        // Act
        var result = await _challengeApplication.RespondChallengeAsync(QuinielaId, ChallengeId, RivalUserId, dto);

        // Assert
        var rival = challenge.Participants.Single(p => p.MemberId == RivalMemberId);
        rival.Status.Should().Be("DECLINED");
        rival.RespondedAtUtc.Should().NotBeNull();
        challenge.Status.Should().Be("PENDING_ACCEPTANCE");
        result.Participants.Single(p => p.MemberId == RivalMemberId).Status.Should().Be("DECLINED");

        _unitOfWorkMock.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
        _webPushMock.Verify(w => w.SendNotificationToUserAsync(
                CreatorUserId,
                It.Is<PushNotificationPayload>(p => p.Title.Contains("Respuesta a tu Reto")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RespondChallengeAsync_WeekLocked_ThrowsInvalidOperation()
    {
        // Arrange
        var week = BuildOpenWeek();
        week.Status = "LOCKED";
        SetupWeek(week, BuildMatch());
        var challenge = BuildChallenge("WEEKLY_RANK", "PENDING_ACCEPTANCE", week,
            Participant(CreatorMemberId, "ACCEPTED"),
            Participant(RivalMemberId, "PENDING"));
        SetupChallenge(challenge);

        // Act
        var act = () => _challengeApplication.RespondChallengeAsync(QuinielaId, ChallengeId, RivalUserId, new RespondChallengeDto { Action = "ACCEPT" });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*bloqueada*");
        challenge.Participants.Single(p => p.MemberId == RivalMemberId).Status.Should().Be("PENDING");
    }

    #endregion

    #region SettleDebtAsync

    private QuinielaChallenge SetupResolvedChallenge()
    {
        var week = BuildOpenWeek();
        week.Status = "SCORED";
        SetupWeek(week, BuildMatch("post", "AME"));
        var challenge = BuildChallenge("WEEKLY_RANK", "RESOLVED_WITH_WINNER", week,
            Participant(CreatorMemberId, "ACCEPTED", "WINNER"),
            Participant(RivalMemberId, "ACCEPTED", "LOSER"));
        SetupChallenge(challenge);
        return challenge;
    }

    [Fact]
    public async Task SettleDebtAsync_WinnerMarksLoserDebt_SetsSettledFlagAndTimestamp()
    {
        // Arrange
        var challenge = SetupResolvedChallenge();
        var before = DateTime.UtcNow;

        // Act
        var result = await _challengeApplication.SettleDebtAsync(QuinielaId, ChallengeId, RivalMemberId, CreatorUserId, isAdmin: false);

        // Assert
        var loser = challenge.Participants.Single(p => p.MemberId == RivalMemberId);
        loser.IsDebtSettled.Should().BeTrue();
        loser.SettledAtUtc.Should().NotBeNull().And.BeOnOrAfter(before);
        result.Participants.Single(p => p.MemberId == RivalMemberId).IsDebtSettled.Should().BeTrue();

        _challengeRepoMock.Verify(r => r.UpdateAsync(challenge), Times.Once);
        _unitOfWorkMock.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SettleDebtAsync_GlobalAdminNotParticipant_SetsSettledFlag()
    {
        // Arrange
        var challenge = SetupResolvedChallenge();

        // Act
        await _challengeApplication.SettleDebtAsync(QuinielaId, ChallengeId, RivalMemberId, ThirdUserId, isAdmin: true);

        // Assert
        var loser = challenge.Participants.Single(p => p.MemberId == RivalMemberId);
        loser.IsDebtSettled.Should().BeTrue();
        loser.SettledAtUtc.Should().NotBeNull();
        _unitOfWorkMock.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SettleDebtAsync_QuinielaAdminRole_SetsSettledFlag()
    {
        // Arrange
        var challenge = SetupResolvedChallenge();

        // Act
        await _challengeApplication.SettleDebtAsync(QuinielaId, ChallengeId, RivalMemberId, FourthUserId, isAdmin: false);

        // Assert
        challenge.Participants.Single(p => p.MemberId == RivalMemberId).IsDebtSettled.Should().BeTrue();
    }

    [Theory]
    [InlineData(RivalUserId)]
    [InlineData(ThirdUserId)]
    public async Task SettleDebtAsync_NonWinnerNonAdmin_ThrowsUnauthorizedAccess(int currentUserId)
    {
        // Arrange
        var challenge = SetupResolvedChallenge();

        // Act
        var act = () => _challengeApplication.SettleDebtAsync(QuinielaId, ChallengeId, RivalMemberId, currentUserId, isAdmin: false);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        var loser = challenge.Participants.Single(p => p.MemberId == RivalMemberId);
        loser.IsDebtSettled.Should().BeFalse();
        loser.SettledAtUtc.Should().BeNull();
        _unitOfWorkMock.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region CancelChallengeAsync

    [Fact]
    public async Task CancelChallengeAsync_CreatorWhilePending_CancelsChallenge()
    {
        // Arrange
        var week = BuildOpenWeek();
        SetupWeek(week, BuildMatch());
        var challenge = BuildChallenge("WEEKLY_RANK", "PENDING_ACCEPTANCE", week,
            Participant(CreatorMemberId, "ACCEPTED"),
            Participant(RivalMemberId, "PENDING"));
        SetupChallenge(challenge);

        // Act
        var result = await _challengeApplication.CancelChallengeAsync(QuinielaId, ChallengeId, CreatorUserId);

        // Assert
        result.Should().BeTrue();
        challenge.Status.Should().Be("CANCELLED");
        challenge.LastModified.Should().NotBeNull();
        _challengeRepoMock.Verify(r => r.UpdateAsync(challenge), Times.Once);
        _unitOfWorkMock.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelChallengeAsync_ChallengeAlreadyActive_ThrowsInvalidOperation()
    {
        // Arrange
        var week = BuildOpenWeek();
        SetupWeek(week, BuildMatch());
        var challenge = BuildChallenge("WEEKLY_RANK", "ACTIVE", week,
            Participant(CreatorMemberId, "ACCEPTED"),
            Participant(RivalMemberId, "ACCEPTED"));
        SetupChallenge(challenge);

        // Act
        var act = () => _challengeApplication.CancelChallengeAsync(QuinielaId, ChallengeId, CreatorUserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ningún invitado lo haya aceptado*");
        challenge.Status.Should().Be("ACTIVE");
        _unitOfWorkMock.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelChallengeAsync_NonCreatorMember_ThrowsUnauthorizedAccess()
    {
        // Arrange
        var week = BuildOpenWeek();
        SetupWeek(week, BuildMatch());
        var challenge = BuildChallenge("WEEKLY_RANK", "PENDING_ACCEPTANCE", week,
            Participant(CreatorMemberId, "ACCEPTED"),
            Participant(RivalMemberId, "PENDING"));
        SetupChallenge(challenge);

        // Act
        var act = () => _challengeApplication.CancelChallengeAsync(QuinielaId, ChallengeId, RivalUserId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        challenge.Status.Should().Be("PENDING_ACCEPTANCE");
    }

    #endregion

    #region ExpireUnacceptedChallengesAsync

    [Fact]
    public async Task ExpireUnacceptedChallengesAsync_PendingChallenge_ExpiresChallengeAndPendingParticipants()
    {
        // Arrange
        var week = BuildOpenWeek();
        var challenge = BuildChallenge("WEEKLY_RANK", "PENDING_ACCEPTANCE", week,
            Participant(CreatorMemberId, "ACCEPTED"),
            Participant(RivalMemberId, "PENDING"),
            Participant(ThirdMemberId, "DECLINED"));
        _challengeRepoMock.Setup(r => r.GetActiveOrPendingChallengesByWeekAsync(QuinielaId, WeekId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QuinielaChallenge> { challenge });

        // Act
        await _challengeApplication.ExpireUnacceptedChallengesAsync(QuinielaId, WeekId);

        // Assert
        challenge.Status.Should().Be("UNACCEPTED_EXPIRED");
        challenge.ResolutionSummary.Should().NotBeNullOrWhiteSpace();
        challenge.Participants.Single(p => p.MemberId == RivalMemberId).Status.Should().Be("EXPIRED");
        challenge.Participants.Single(p => p.MemberId == CreatorMemberId).Status.Should().Be("ACCEPTED");
        challenge.Participants.Single(p => p.MemberId == ThirdMemberId).Status.Should().Be("DECLINED");
        _challengeRepoMock.Verify(r => r.UpdateAsync(challenge), Times.Once);
        _unitOfWorkMock.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpireUnacceptedChallengesAsync_ActiveChallengeWithPendingInvite_ExpiresOnlyPendingParticipant()
    {
        // Arrange
        var week = BuildOpenWeek();
        var challenge = BuildChallenge("WEEKLY_RANK", "ACTIVE", week,
            Participant(CreatorMemberId, "ACCEPTED"),
            Participant(RivalMemberId, "ACCEPTED"),
            Participant(ThirdMemberId, "PENDING"));
        _challengeRepoMock.Setup(r => r.GetActiveOrPendingChallengesByWeekAsync(QuinielaId, WeekId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QuinielaChallenge> { challenge });

        // Act
        await _challengeApplication.ExpireUnacceptedChallengesAsync(QuinielaId, WeekId);

        // Assert
        challenge.Status.Should().Be("ACTIVE");
        challenge.Participants.Single(p => p.MemberId == ThirdMemberId).Status.Should().Be("EXPIRED");
        challenge.Participants.Count(p => p.Status == "ACCEPTED").Should().Be(2);
        _unitOfWorkMock.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpireUnacceptedChallengesAsync_NoChallenges_DoesNotSave()
    {
        // Arrange (repositorio por defecto sin retos)

        // Act
        await _challengeApplication.ExpireUnacceptedChallengesAsync(QuinielaId, WeekId);

        // Assert
        _challengeRepoMock.Verify(r => r.UpdateAsync(It.IsAny<QuinielaChallenge>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region ResolveWeekChallengesAsync

    [Fact]
    public async Task ResolveWeekChallengesAsync_ActiveMatchWinner_ResolvesWithWinnerAndLoser()
    {
        // Arrange
        var week = BuildOpenWeek();
        week.Status = "SCORED";
        SetupWeek(week, BuildMatch("post", "AME"));

        var creator = Participant(CreatorMemberId, "ACCEPTED");
        creator.PredictedWinnerAbbr = "AME";
        var rival = Participant(RivalMemberId, "ACCEPTED");
        rival.PredictedWinnerAbbr = "GDL";
        var challenge = BuildChallenge("MATCH_WINNER", "ACTIVE", week, creator, rival);
        _challengeRepoMock.Setup(r => r.GetActiveOrPendingChallengesByWeekAsync(QuinielaId, WeekId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QuinielaChallenge> { challenge });

        // Act
        await _challengeApplication.ResolveWeekChallengesAsync(QuinielaId, WeekId);

        // Assert
        challenge.Status.Should().Be("RESOLVED_WITH_WINNER");
        challenge.ResolvedAtUtc.Should().NotBeNull();
        challenge.ResolutionSummary.Should().Contain("ElRetador");
        challenge.DoubleLifeEventsJson.Should().BeNull();
        creator.Outcome.Should().Be("WINNER");
        rival.Outcome.Should().Be("LOSER");
        _challengeRepoMock.Verify(r => r.UpdateAsync(challenge), Times.Once);
        _unitOfWorkMock.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveWeekChallengesAsync_NobodyHitsTotalGoals_ResolvesWithNoWinner()
    {
        // Arrange
        var week = BuildOpenWeek();
        week.Status = "SCORED";
        var match = BuildMatch("post", "AME");
        match.HomeScore = 1;
        match.AwayScore = 0;
        SetupWeek(week, match);

        var creator = Participant(CreatorMemberId, "ACCEPTED");
        creator.PredictedTotalGoals = 5;
        var rival = Participant(RivalMemberId, "ACCEPTED");
        rival.PredictedTotalGoals = 8;
        var challenge = BuildChallenge("TOTAL_GOALS", "ACTIVE", week, creator, rival);
        _challengeRepoMock.Setup(r => r.GetActiveOrPendingChallengesByWeekAsync(QuinielaId, WeekId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QuinielaChallenge> { challenge });

        // Act
        await _challengeApplication.ResolveWeekChallengesAsync(QuinielaId, WeekId);

        // Assert
        challenge.Status.Should().Be("RESOLVED_NO_WINNER");
        creator.Outcome.Should().Be("NO_WINNER_BUST");
        rival.Outcome.Should().Be("NO_WINNER_BUST");
        _unitOfWorkMock.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveWeekChallengesAsync_PendingChallenge_ExpiresInsteadOfResolving()
    {
        // Arrange
        var week = BuildOpenWeek();
        week.Status = "SCORED";
        SetupWeek(week, BuildMatch("post", "AME"));
        var challenge = BuildChallenge("WEEKLY_RANK", "PENDING_ACCEPTANCE", week,
            Participant(CreatorMemberId, "ACCEPTED"),
            Participant(RivalMemberId, "PENDING"));
        _challengeRepoMock.Setup(r => r.GetActiveOrPendingChallengesByWeekAsync(QuinielaId, WeekId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QuinielaChallenge> { challenge });

        // Act
        await _challengeApplication.ResolveWeekChallengesAsync(QuinielaId, WeekId);

        // Assert
        challenge.Status.Should().Be("UNACCEPTED_EXPIRED");
        challenge.ResolvedAtUtc.Should().BeNull();
        challenge.Participants.Single(p => p.MemberId == RivalMemberId).Status.Should().Be("EXPIRED");
        _unitOfWorkMock.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
