using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using UseCases.Security;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class AvatarStorageServiceTests : IDisposable
{
    private readonly string _testStorageDir;
    private readonly AvatarStorageService _service;

    public AvatarStorageServiceTests()
    {
        _testStorageDir = Path.Combine(Path.GetTempPath(), "picksports_test_avatars_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testStorageDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AvatarStorage:Path", _testStorageDir }
            })
            .Build();

        _service = new AvatarStorageService(config, NullLogger<AvatarStorageService>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testStorageDir))
            {
                Directory.Delete(_testStorageDir, true);
            }
        }
        catch
        {
            // Limpieza de directorio temporal
        }
    }

    [Theory]
    [InlineData(".jpg", true)]
    [InlineData(".jpeg", true)]
    [InlineData(".png", true)]
    [InlineData(".webp", true)]
    [InlineData(".gif", true)]
    [InlineData(".exe", false)]
    [InlineData(".php", false)]
    [InlineData(".sh", false)]
    [InlineData(".svg", false)]
    public void IsAllowedExtension_ValidatesWhitelistCorrectly(string extension, bool expected)
    {
        // Act
        var result = _service.IsAllowedExtension(extension);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsValidImageSignature_RecognizesJpegMagicBytes()
    {
        // Arrange
        byte[] jpegHeader = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };

        // Act
        var isValid = _service.IsValidImageSignature(jpegHeader, ".jpg");

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValidImageSignature_RecognizesPngMagicBytes()
    {
        // Arrange
        byte[] pngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        // Act
        var isValid = _service.IsValidImageSignature(pngHeader, ".png");

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValidImageSignature_RejectsSpoofedExtension()
    {
        // Arrange: Texto plano ASCII con extensión .png
        byte[] fakeHeader = { 0x3C, 0x21, 0x44, 0x4F, 0x43, 0x54, 0x59, 0x50 }; // "<!DOCTYP"

        // Act
        var isValid = _service.IsValidImageSignature(fakeHeader, ".png");

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task SaveAvatarAsync_WithValidPng_SavesFileAndReturnsApiUrl()
    {
        // Arrange
        byte[] pngContent = { 
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01
        };
        using var stream = new MemoryStream(pngContent);

        // Act
        var avatarUrl = await _service.SaveAvatarAsync(42, stream, "test_foto.png", "image/png");

        // Assert
        Assert.StartsWith("/api/auth/avatar/avatar_u42_", avatarUrl);
        Assert.EndsWith(".png", avatarUrl);

        var fileName = Path.GetFileName(avatarUrl);
        var diskPath = Path.Combine(_testStorageDir, fileName);
        Assert.True(File.Exists(diskPath));
    }

    [Fact]
    public async Task SaveAvatarAsync_WithInvalidExtension_ThrowsArgumentException()
    {
        // Arrange
        byte[] dummyBytes = { 0x01, 0x02, 0x03, 0x04 };
        using var stream = new MemoryStream(dummyBytes);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveAvatarAsync(1, stream, "hack.exe", "application/octet-stream"));
    }

    [Fact]
    public void GetAvatarFilePath_RejectsDirectoryTraversalAttempts()
    {
        // Act
        var result1 = _service.GetAvatarFilePath("../secret.txt");
        var result2 = _service.GetAvatarFilePath("..\\..\\windows\\win.ini");
        var result3 = _service.GetAvatarFilePath("sub/folder/file.jpg");

        // Assert
        Assert.Null(result1);
        Assert.Null(result2);
        Assert.Null(result3);
    }

    [Fact]
    public async Task DeleteAvatarAsync_RemovesExistingFile()
    {
        // Arrange
        var testFile = Path.Combine(_testStorageDir, "avatar_u1_abc123.jpg");
        await File.WriteAllTextAsync(testFile, "test content");

        // Act
        await _service.DeleteAvatarAsync("/api/auth/avatar/avatar_u1_abc123.jpg");

        // Assert
        Assert.False(File.Exists(testFile));
    }
}
