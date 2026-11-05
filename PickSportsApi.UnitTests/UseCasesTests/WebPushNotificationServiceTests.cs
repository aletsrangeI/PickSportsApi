using System.Net;
using Common;
using Domain.Entities;
using DTO.Notifications;
using Interface.Persistence;
using Microsoft.Extensions.Configuration;
using Moq;
using UseCases.Notifications;
using Validator.Notifications;
using WebPush;
using Xunit;
using DbPushSubscription = Domain.Entities.PushSubscription;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class WebPushNotificationServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUow = new();
    private readonly Mock<IAppLogger<WebPushNotificationService>> _mockLogger = new();
    private readonly VapidDetails _vapidKeys;

    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public WebPushNotificationServiceTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
        var keys = VapidHelper.GenerateVapidKeys();
        _vapidKeys = new VapidDetails("mailto:admin@picksports.orionsys.net", keys.PublicKey, keys.PrivateKey);
    }

    [Fact]
    public void VapidHelper_ShouldGenerateValidKeys()
    {
        var keys = VapidHelper.GenerateVapidKeys();
        _output.WriteLine($"GENERATED_PUBLIC_KEY: {keys.PublicKey}");
        _output.WriteLine($"GENERATED_PRIVATE_KEY: {keys.PrivateKey}");
        Assert.NotNull(keys);
        Assert.False(string.IsNullOrWhiteSpace(keys.PublicKey));
        Assert.False(string.IsNullOrWhiteSpace(keys.PrivateKey));
    }

    [Fact]
    public void GetVapidPublicKey_WhenConfigured_ShouldReturnKey()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Vapid:Subject", _vapidKeys.Subject },
            { "Vapid:PublicKey", _vapidKeys.PublicKey },
            { "Vapid:PrivateKey", _vapidKeys.PrivateKey }
        };
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var service = new WebPushNotificationService(_mockUow.Object, config, _mockLogger.Object);

        // Act
        var publicKey = service.GetVapidPublicKey();

        // Assert
        Assert.Equal(_vapidKeys.PublicKey, publicKey);
    }

    [Fact]
    public async Task SubscribeAsync_WhenValid_ShouldInsertOrUpdateSubscription()
    {
        // Arrange
        var mockSubRepo = new Mock<IPushSubscriptionRepository>();
        var mockUserRepo = new Mock<IUserRepository>();

        _mockUow.Setup(u => u.PushSubscriptions).Returns(mockSubRepo.Object);
        _mockUow.Setup(u => u.Users).Returns(mockUserRepo.Object);

        mockUserRepo.Setup(r => r.GetAsync(1)).ReturnsAsync(new User { Id = 1, Username = "testuser" });
        mockSubRepo.Setup(r => r.GetByEndpointAsync("https://push.example.com/sub/123"))
            .ReturnsAsync((DbPushSubscription?)null);

        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Vapid:Subject", _vapidKeys.Subject },
            { "Vapid:PublicKey", _vapidKeys.PublicKey },
            { "Vapid:PrivateKey", _vapidKeys.PrivateKey }
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var pushService = new WebPushNotificationService(_mockUow.Object, config, _mockLogger.Object);
        var validator = new PushSubscriptionRequestValidator();
        var app = new NotificationsApplication(_mockUow.Object, pushService, validator);

        var request = new PushSubscriptionRequestDto
        {
            Endpoint = "https://push.example.com/sub/123",
            P256dhKey = "test-p256dh-key-base64",
            AuthKey = "test-auth-key-base64",
            DeviceDescription = "Chrome on Windows"
        };

        // Act
        var result = await app.SubscribeAsync(1, request);

        // Assert
        Assert.True(result.isSuccess);
        mockSubRepo.Verify(r => r.InsertAsync(It.Is<DbPushSubscription>(s =>
            s.UserId == 1 &&
            s.Endpoint == "https://push.example.com/sub/123" &&
            s.Active)), Times.Once);
        _mockUow.Verify(u => u.Save(default), Times.Once);
    }

    [Fact]
    public async Task UnsubscribeAsync_WhenCalled_ShouldDeactivateSubscription()
    {
        // Arrange
        var mockSubRepo = new Mock<IPushSubscriptionRepository>();
        _mockUow.Setup(u => u.PushSubscriptions).Returns(mockSubRepo.Object);

        var existingSub = new DbPushSubscription
        {
            Id = 5,
            UserId = 1,
            Endpoint = "https://push.example.com/sub/123",
            Active = true
        };
        mockSubRepo.Setup(r => r.GetByEndpointAsync("https://push.example.com/sub/123"))
            .ReturnsAsync(existingSub);

        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Vapid:PublicKey", _vapidKeys.PublicKey },
            { "Vapid:PrivateKey", _vapidKeys.PrivateKey }
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var pushService = new WebPushNotificationService(_mockUow.Object, config, _mockLogger.Object);
        var validator = new PushSubscriptionRequestValidator();
        var app = new NotificationsApplication(_mockUow.Object, pushService, validator);

        // Act
        var result = await app.UnsubscribeAsync(1, new UnsubscribePushRequestDto
        {
            Endpoint = "https://push.example.com/sub/123"
        });

        // Assert
        Assert.True(result.isSuccess);
        Assert.False(existingSub.Active);
        mockSubRepo.Verify(r => r.UpdateAsync(existingSub), Times.Once);
        _mockUow.Verify(u => u.Save(default), Times.Once);
    }

    [Fact]
    public void PushSubscriptionRequestValidator_WhenInvalidEndpoint_ShouldFail()
    {
        var validator = new PushSubscriptionRequestValidator();
        var request = new PushSubscriptionRequestDto
        {
            Endpoint = "invalid-not-a-url",
            P256dhKey = "key",
            AuthKey = "auth"
        };

        var result = validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Endpoint");
    }

    [Fact]
    public void PushSubscriptionRequestValidator_WithNestedKeys_ShouldBeValid()
    {
        var validator = new PushSubscriptionRequestValidator();
        var request = new PushSubscriptionRequestDto
        {
            Endpoint = "https://fcm.googleapis.com/fcm/send/abc123xyz",
            Keys = new PushSubscriptionKeysDto
            {
                P256dh = "valid-p256dh",
                Auth = "valid-auth"
            }
        };

        var result = validator.Validate(request);
        Assert.True(result.IsValid);
        Assert.Equal("valid-p256dh", request.GetP256dh());
        Assert.Equal("valid-auth", request.GetAuth());
    }

    // ---------- SPEC-023: Push:Enabled y Push:AllowedUserIds ----------

    private static readonly PushNotificationPayload TestPayload = new("titulo", "mensaje");

    private WebPushNotificationService CreateServiceWith(
        Mock<IPushSubscriptionRepository> subRepo, params (string Key, string? Value)[] extra)
    {
        _mockUow.Setup(u => u.PushSubscriptions).Returns(subRepo.Object);

        var settings = new Dictionary<string, string?>
        {
            { "Vapid:Subject", _vapidKeys.Subject },
            { "Vapid:PublicKey", _vapidKeys.PublicKey },
            { "Vapid:PrivateKey", _vapidKeys.PrivateKey }
        };
        foreach (var (key, value) in extra) settings[key] = value;

        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new WebPushNotificationService(_mockUow.Object, config, _mockLogger.Object);
    }

    [Fact]
    public async Task Send_WhenPushDisabledByConfig_ReturnsZeroAndNeverQueriesSubscriptions()
    {
        // Arrange
        var subRepo = new Mock<IPushSubscriptionRepository>();
        var service = CreateServiceWith(subRepo, ("Push:Enabled", "false"));

        // Act
        var toUser = await service.SendNotificationToUserAsync(1, TestPayload);
        var toUsers = await service.SendNotificationToUsersAsync(new[] { 1, 2 }, TestPayload);
        var toQuiniela = await service.SendNotificationToQuinielaAsync(10, TestPayload);

        // Assert
        Assert.Equal(0, toUser);
        Assert.Equal(0, toUsers);
        Assert.Equal(0, toQuiniela);
        subRepo.Verify(r => r.GetActiveSubscriptionsByUserAsync(It.IsAny<int>()), Times.Never);
        subRepo.Verify(r => r.GetActiveSubscriptionsByQuinielaAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Send_WhenPushKeyAbsent_StillQueriesSubscriptions()
    {
        // Arrange: producción no define Push:*, debe comportarse como antes.
        var subRepo = new Mock<IPushSubscriptionRepository>();
        subRepo.Setup(r => r.GetActiveSubscriptionsByUserAsync(1))
            .ReturnsAsync(new List<DbPushSubscription>());
        var service = CreateServiceWith(subRepo);

        // Act
        var result = await service.SendNotificationToUserAsync(1, TestPayload);

        // Assert
        Assert.Equal(0, result);
        subRepo.Verify(r => r.GetActiveSubscriptionsByUserAsync(1), Times.Once);
    }

    [Fact]
    public async Task SendToUser_WhenUserNotInAllowlist_ReturnsZeroWithoutQueryingSubscriptions()
    {
        // Arrange
        var subRepo = new Mock<IPushSubscriptionRepository>();
        var service = CreateServiceWith(subRepo, ("Push:AllowedUserIds:0", "1"));

        // Act
        var result = await service.SendNotificationToUserAsync(2, TestPayload);

        // Assert
        Assert.Equal(0, result);
        subRepo.Verify(r => r.GetActiveSubscriptionsByUserAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SendToUser_WhenUserInAllowlist_QueriesSubscriptions()
    {
        // Arrange
        var subRepo = new Mock<IPushSubscriptionRepository>();
        subRepo.Setup(r => r.GetActiveSubscriptionsByUserAsync(1))
            .ReturnsAsync(new List<DbPushSubscription>());
        var service = CreateServiceWith(subRepo, ("Push:AllowedUserIds:0", "1"));

        // Act
        await service.SendNotificationToUserAsync(1, TestPayload);

        // Assert
        subRepo.Verify(r => r.GetActiveSubscriptionsByUserAsync(1), Times.Once);
    }

    [Fact]
    public async Task SendToQuiniela_FiltersSubscriptionsOfUsersOutsideTheAllowlist()
    {
        // Arrange: la quiniela tiene suscripciones de los usuarios 2 y 3; solo el 1 está permitido.
        var subRepo = new Mock<IPushSubscriptionRepository>();
        subRepo.Setup(r => r.GetActiveSubscriptionsByQuinielaAsync(10)).ReturnsAsync(new List<DbPushSubscription>
        {
            // Llaves inválidas a propósito: si el filtro fallara y se intentara enviar, el envío falla al instante
            // (sin tocar la red) y queda registrado con LogError, que es lo que se verifica abajo.
            new() { Id = 1, UserId = 2, Endpoint = "https://push.example.com/2", P256dhKey = "!!", AuthKey = "!!", Active = true },
            new() { Id = 2, UserId = 3, Endpoint = "https://push.example.com/3", P256dhKey = "!!", AuthKey = "!!", Active = true }
        });
        var service = CreateServiceWith(subRepo, ("Push:AllowedUserIds:0", "1"));

        // Act
        var result = await service.SendNotificationToQuinielaAsync(10, TestPayload);

        // Assert: no se intentó enviar a nadie (ningún error de envío) ni se tocó ninguna suscripción.
        Assert.Equal(0, result);
        _mockLogger.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<object[]>()), Times.Never);
        subRepo.Verify(r => r.UpdateAsync(It.IsAny<DbPushSubscription>()), Times.Never);
        subRepo.Verify(r => r.DeactivateEndpointAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendToQuiniela_PositiveControl_AttemptsSendOnlyForAllowedUsers()
    {
        // Arrange: usuarios 1 (permitido) y 2 (no). Las llaves inválidas hacen que el intento falle sin red.
        var subRepo = new Mock<IPushSubscriptionRepository>();
        subRepo.Setup(r => r.GetActiveSubscriptionsByQuinielaAsync(10)).ReturnsAsync(new List<DbPushSubscription>
        {
            new() { Id = 1, UserId = 1, Endpoint = "https://push.example.com/1", P256dhKey = "!!", AuthKey = "!!", Active = true },
            new() { Id = 2, UserId = 2, Endpoint = "https://push.example.com/2", P256dhKey = "!!", AuthKey = "!!", Active = true }
        });
        var service = CreateServiceWith(subRepo, ("Push:AllowedUserIds:0", "1"));

        // Act
        var result = await service.SendNotificationToQuinielaAsync(10, TestPayload);

        // Assert: exactamente un intento de envío (el del usuario 1); el del usuario 2 se filtró.
        Assert.Equal(0, result);
        _mockLogger.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once);
    }

    [Theory]
    [InlineData("no")]
    [InlineData("0")]
    [InlineData("off")]
    public async Task Send_WhenPushEnabledIsUnparseable_TurnsPushOffInsteadOfFailingOpen(string value)
    {
        // Arrange
        var subRepo = new Mock<IPushSubscriptionRepository>();
        var service = CreateServiceWith(subRepo, ("Push:Enabled", value));

        // Act
        var result = await service.SendNotificationToUserAsync(1, TestPayload);

        // Assert
        Assert.Equal(0, result);
        subRepo.Verify(r => r.GetActiveSubscriptionsByUserAsync(It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [InlineData("1;5")]
    [InlineData("usuario1")]
    [InlineData("[1]")]
    public async Task Send_WhenAllowlistHasNoValidIds_SendsToNobodyInsteadOfEveryone(string value)
    {
        // Arrange: la clave está configurada pero no produce ningún id válido => falla cerrado.
        var subRepo = new Mock<IPushSubscriptionRepository>();
        var service = CreateServiceWith(subRepo, ("Push:AllowedUserIds", value));

        // Act
        var result = await service.SendNotificationToUserAsync(1, TestPayload);

        // Assert
        Assert.Equal(0, result);
        subRepo.Verify(r => r.GetActiveSubscriptionsByUserAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SendToUsers_InheritsTheAllowlist()
    {
        // Arrange
        var subRepo = new Mock<IPushSubscriptionRepository>();
        subRepo.Setup(r => r.GetActiveSubscriptionsByUserAsync(1)).ReturnsAsync(new List<DbPushSubscription>());
        var service = CreateServiceWith(subRepo, ("Push:AllowedUserIds", "1,5"));

        // Act
        var result = await service.SendNotificationToUsersAsync(new[] { 1, 2, 3 }, TestPayload);

        // Assert: solo se consulta al usuario 1 (la lista separada por comas también funciona).
        Assert.Equal(0, result);
        subRepo.Verify(r => r.GetActiveSubscriptionsByUserAsync(1), Times.Once);
        subRepo.Verify(r => r.GetActiveSubscriptionsByUserAsync(2), Times.Never);
        subRepo.Verify(r => r.GetActiveSubscriptionsByUserAsync(3), Times.Never);
    }
}
