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
}
