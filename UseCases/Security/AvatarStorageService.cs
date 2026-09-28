using System.Text.RegularExpressions;
using Domain.Entities;
using Interface.Persistence;
using Interface.UseCases;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace UseCases.Security;

public class AvatarStorageService : IAvatarStorageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png", "image/png" },
        { ".webp", "image/webp" },
        { ".gif", "image/gif" }
    };

    private readonly string _storageDir;
    private readonly ILogger<AvatarStorageService> _logger;
    private readonly IUnitOfWork? _unitOfWork;

    public AvatarStorageService(
        IConfiguration configuration,
        ILogger<AvatarStorageService> logger,
        IUnitOfWork? unitOfWork = null)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;

        var configuredPath = configuration["AvatarStorage:Path"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            _storageDir = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(AppContext.BaseDirectory, configuredPath);
        }
        else
        {
            _storageDir = Path.Combine(AppContext.BaseDirectory, "uploads", "avatars");
        }

        try
        {
            if (!Directory.Exists(_storageDir))
            {
                Directory.CreateDirectory(_storageDir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo crear el directorio de avatares en {StorageDir}", _storageDir);
        }
    }

    public bool IsAllowedExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return false;
        var ext = extension.StartsWith('.') ? extension : $".{extension}";
        return AllowedExtensions.Contains(ext);
    }

    public bool IsValidImageSignature(byte[] headerBytes, string extension)
    {
        if (headerBytes == null || headerBytes.Length < 4) return false;

        var ext = extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";

        if (ext == ".jpg" || ext == ".jpeg")
        {
            // JPEG: FF D8 FF
            return headerBytes.Length >= 3 &&
                   headerBytes[0] == 0xFF &&
                   headerBytes[1] == 0xD8 &&
                   headerBytes[2] == 0xFF;
        }

        if (ext == ".png")
        {
            // PNG: 89 50 4E 47 0D 0A 1A 0A
            return headerBytes.Length >= 8 &&
                   headerBytes[0] == 0x89 &&
                   headerBytes[1] == 0x50 &&
                   headerBytes[2] == 0x4E &&
                   headerBytes[3] == 0x47 &&
                   headerBytes[4] == 0x0D &&
                   headerBytes[5] == 0x0A &&
                   headerBytes[6] == 0x1A &&
                   headerBytes[7] == 0x0A;
        }

        if (ext == ".gif")
        {
            // GIF: "GIF87a" o "GIF89a" -> 47 49 46 38
            return headerBytes.Length >= 4 &&
                   headerBytes[0] == 0x47 &&
                   headerBytes[1] == 0x49 &&
                   headerBytes[2] == 0x46 &&
                   headerBytes[3] == 0x38;
        }

        if (ext == ".webp")
        {
            // WEBP: "RIFF" .... "WEBP"
            return headerBytes.Length >= 12 &&
                   headerBytes[0] == 0x52 && headerBytes[1] == 0x49 && headerBytes[2] == 0x46 && headerBytes[3] == 0x46 &&
                   headerBytes[8] == 0x57 && headerBytes[9] == 0x45 && headerBytes[10] == 0x42 && headerBytes[11] == 0x50;
        }

        return false;
    }

    public async Task<string> SaveAvatarAsync(
        int userId,
        Stream fileStream,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext) || !IsAllowedExtension(ext))
        {
            throw new ArgumentException("Formato de imagen no permitido. Se aceptan: JPG, PNG, WEBP o GIF.");
        }

        // Leer primeros 16 bytes para verificar la firma real (magic bytes)
        var header = new byte[16];
        int bytesRead = await fileStream.ReadAsync(header, 0, header.Length, cancellationToken);
        if (bytesRead < 4 || !IsValidImageSignature(header, ext))
        {
            throw new ArgumentException("El contenido del archivo no corresponde a una imagen válida.");
        }

        if (!Directory.Exists(_storageDir))
        {
            Directory.CreateDirectory(_storageDir);
        }

        // Generar nombre seguro e impredecible
        var safeFileName = $"avatar_u{userId}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(_storageDir, safeFileName);

        // Copiar el archivo completo a memoria para guardar en disco Y en base de datos PostgreSQL
        byte[] fileBytes;
        await using (var ms = new MemoryStream())
        {
            await ms.WriteAsync(header.AsMemory(0, bytesRead), cancellationToken);
            await fileStream.CopyToAsync(ms, cancellationToken);
            fileBytes = ms.ToArray();
        }

        // 1. Guardar en disco local para acceso ultrarrápido
        await File.WriteAllBytesAsync(fullPath, fileBytes, cancellationToken);

        // 2. Persistir en PostgreSQL para que NUNCA se pierda por despliegues Docker, reinicios o limpiezas
        if (_unitOfWork != null)
        {
            try
            {
                var existing = await _unitOfWork.UserAvatarFiles.GetByUserIdAsync(userId, cancellationToken);
                var resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? GetContentType(safeFileName) : contentType;

                if (existing != null)
                {
                    existing.FileName = safeFileName;
                    existing.ContentType = resolvedContentType;
                    existing.Data = fileBytes;
                    existing.CreatedAt = DateTime.UtcNow;
                    await _unitOfWork.UserAvatarFiles.UpdateAsync(existing);
                }
                else
                {
                    await _unitOfWork.UserAvatarFiles.InsertAsync(new UserAvatarFile
                    {
                        UserId = userId,
                        FileName = safeFileName,
                        ContentType = resolvedContentType,
                        Data = fileBytes,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                _logger.LogInformation("Avatar persistido en base de datos PostgreSQL para usuario {UserId} ({FileName})", userId, safeFileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo persistir avatar en PostgreSQL para usuario {UserId}", userId);
            }
        }

        _logger.LogInformation("Avatar guardado para usuario {UserId} en {Path}", userId, fullPath);

        // Retornar la URL relativa servida por el endpoint de la API
        return $"/api/auth/avatar/{safeFileName}";
    }

    public async Task DeleteAvatarAsync(string? avatarUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl)) return;

        try
        {
            const string prefix = "/api/auth/avatar/";
            var idx = avatarUrl.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var fileName = avatarUrl.Substring(idx + prefix.Length).Trim();
                if (Regex.IsMatch(fileName, @"^[a-zA-Z0-9_\-\.]+$") && !fileName.Contains(".."))
                {
                    var fullPath = Path.Combine(_storageDir, fileName);
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        _logger.LogInformation("Avatar anterior eliminado de disco: {FilePath}", fullPath);
                    }

                    if (_unitOfWork != null)
                    {
                        await _unitOfWork.UserAvatarFiles.DeleteByFileNameAsync(fileName, cancellationToken);
                        _logger.LogInformation("Avatar anterior {FileName} eliminado de PostgreSQL", fileName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al intentar eliminar avatar anterior en {AvatarUrl}", avatarUrl);
        }
    }

    public async Task<string?> GetAvatarFilePathAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        // Sanitización contra Directory Traversal
        if (!Regex.IsMatch(fileName, @"^[a-zA-Z0-9_\-\.]+$") || fileName.Contains(".."))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(Path.Combine(_storageDir, fileName));
        var fullDir = Path.GetFullPath(_storageDir);

        if (!fullPath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Si ya existe en disco (caché local), servirlo directamente
        if (File.Exists(fullPath))
        {
            return fullPath;
        }

        // Si no está en disco local, restaurar desde la base de datos PostgreSQL
        if (_unitOfWork != null)
        {
            try
            {
                var record = await _unitOfWork.UserAvatarFiles.GetByFileNameAsync(fileName, cancellationToken);
                if (record != null && record.Data.Length > 0)
                {
                    if (!Directory.Exists(_storageDir))
                    {
                        Directory.CreateDirectory(_storageDir);
                    }

                    await File.WriteAllBytesAsync(fullPath, record.Data, cancellationToken);
                    _logger.LogInformation("Avatar {FileName} restaurado exitosamente en caché de disco desde PostgreSQL", fileName);
                    return fullPath;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al intentar restaurar avatar {FileName} desde PostgreSQL", fileName);
            }
        }

        return null;
    }

    public string? GetAvatarFilePath(string fileName)
    {
        return GetAvatarFilePathAsync(fileName).GetAwaiter().GetResult();
    }

    public string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (MimeTypes.TryGetValue(ext, out var mime))
        {
            return mime;
        }
        return "application/octet-stream";
    }
}
