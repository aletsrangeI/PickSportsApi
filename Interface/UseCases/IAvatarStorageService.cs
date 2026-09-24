namespace Interface.UseCases;

public interface IAvatarStorageService
{
    Task<string> SaveAvatarAsync(int userId, Stream fileStream, string originalFileName, string contentType, CancellationToken cancellationToken = default);
    Task DeleteAvatarAsync(string? avatarUrl, CancellationToken cancellationToken = default);
    string? GetAvatarFilePath(string fileName);
    string GetContentType(string fileName);
    bool IsAllowedExtension(string extension);
    bool IsValidImageSignature(byte[] headerBytes, string extension);
}
