using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using StorageFileOptions = Supabase.Storage.FileOptions;

namespace Alpha.API.Services;

public sealed class VerificationStorageService
{
    public const long MaximumFileSizeBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp",
            "application/pdf"
        };

    private readonly Supabase.Client _supabase;
    private readonly ILogger<VerificationStorageService> _logger;
    private readonly string _bucket;

    public VerificationStorageService(
        Supabase.Client supabase,
        IConfiguration configuration,
        ILogger<VerificationStorageService> logger)
    {
        _supabase = supabase;
        _logger = logger;

        _bucket =
            configuration["Supabase:VerificationBucket"]
            ?? Environment.GetEnvironmentVariable(
                "SUPABASE_VERIFICATION_BUCKET")
            ?? "role-verifications";
    }

    public string Bucket => _bucket;

    public void ValidateFile(IFormFile file)
    {
        if (file == null || file.Length <= 0)
        {
            throw new InvalidOperationException(
                "Select a document to upload.");
        }

        if (file.Length > MaximumFileSizeBytes)
        {
            throw new InvalidOperationException(
                "The maximum document size is 10 MB.");
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) ||
            !AllowedContentTypes.Contains(file.ContentType))
        {
            throw new InvalidOperationException(
                "Only JPG, PNG, WEBP, and PDF files are allowed.");
        }
    }

    public async Task<string> UploadAsync(
        Guid userId,
        Guid applicationId,
        string roleKey,
        string documentType,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        ValidateFile(file);

        var safeRole = SanitizeSegment(roleKey);
        var safeDocumentType = SanitizeSegment(documentType);
        var extension = GetSafeExtension(
            file.FileName,
            file.ContentType);

        var storedFileName = $"{Guid.NewGuid():N}{extension}";

        var objectPath =
            $"{userId:D}/" +
            $"{applicationId:D}/" +
            $"{safeRole}/" +
            $"{safeDocumentType}/" +
            storedFileName;

        var temporaryFilePath = Path.Combine(
            Path.GetTempPath(),
            $"alpha-verification-{Guid.NewGuid():N}{extension}");

        try
        {
            await using (var temporaryStream =
                new FileStream(
                    temporaryFilePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 64 * 1024,
                    useAsync: true))
            {
                await file.CopyToAsync(
                    temporaryStream,
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            await _supabase.Storage
                .From(_bucket)
                .Upload(
                    temporaryFilePath,
                    objectPath,
                    new StorageFileOptions
                    {
                        CacheControl = "3600",
                        Upsert = false,
                        ContentType = file.ContentType
                    });

            return objectPath;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to upload verification document. Bucket: {Bucket}, Path: {Path}",
                _bucket,
                objectPath);

            throw;
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryFilePath);
        }
    }

    public async Task<byte[]> DownloadAsync(
        string storagePath,
        CancellationToken cancellationToken)
    {
        var objectPath = NormalizeObjectPath(storagePath);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var bytes =
    await _supabase.Storage
        .From(_bucket)
        .Download(
            objectPath,
            null,
            null);

            cancellationToken.ThrowIfCancellationRequested();

            return bytes;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to download verification document. Bucket: {Bucket}, Path: {Path}",
                _bucket,
                objectPath);

            throw;
        }
    }

    public async Task<string> CreateSignedUrlAsync(
        string storagePath,
        int expiresInSeconds = 300)
    {
        if (expiresInSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresInSeconds),
                "Expiration must be greater than zero.");
        }

        var objectPath = NormalizeObjectPath(storagePath);

        try
        {
            return await _supabase.Storage
                .From(_bucket)
                .CreateSignedUrl(
                    objectPath,
                    expiresInSeconds);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to create a signed verification document URL. Bucket: {Bucket}, Path: {Path}",
                _bucket,
                objectPath);

            throw;
        }
    }

    public async Task DeleteAsync(
        string? storagePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return;
        }

        var objectPath = NormalizeObjectPath(storagePath);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await _supabase.Storage
                .From(_bucket)
                .Remove(
                    new List<string>
                    {
                        objectPath
                    });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to delete verification document. Bucket: {Bucket}, Path: {Path}",
                _bucket,
                objectPath);

            throw;
        }
    }

    public string NormalizeObjectPath(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new InvalidOperationException(
                "The document storage path is empty.");
        }

        var normalized = storagePath
            .Trim()
            .Replace("\\", "/");

        if (Uri.TryCreate(
                normalized,
                UriKind.Absolute,
                out var uri))
        {
            normalized = Uri.UnescapeDataString(
                uri.AbsolutePath);
        }

        normalized = normalized.TrimStart('/');

        var knownPrefixes = new[]
        {
            $"storage/v1/object/public/{_bucket}/",
            $"storage/v1/object/sign/{_bucket}/",
            $"storage/v1/object/authenticated/{_bucket}/",
            $"{_bucket}/"
        };

        foreach (var prefix in knownPrefixes)
        {
            var index = normalized.IndexOf(
                prefix,
                StringComparison.OrdinalIgnoreCase);

            if (index < 0)
            {
                continue;
            }

            normalized = normalized[
                (index + prefix.Length)..];

            break;
        }

        normalized = normalized.TrimStart('/');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException(
                "The Supabase Storage object path is empty.");
        }

        if (normalized.StartsWith(
                "app/out/",
                StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(
                "private-uploads/",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "This database record points to an old Railway-local file. The applicant must upload the document again.");
        }

        return normalized;
    }

    private void TryDeleteTemporaryFile(
        string temporaryFilePath)
    {
        try
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to remove temporary verification upload {TemporaryPath}.",
                temporaryFilePath);
        }
    }

    private static string SanitizeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "A valid storage path segment is required.");
        }

        var normalized = value
            .Trim()
            .ToLowerInvariant()
            .Replace("-", "_")
            .Replace(" ", "_");

        normalized = Regex.Replace(
            normalized,
            "[^a-z0-9_]",
            string.Empty);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException(
                "A valid storage path segment is required.");
        }

        return normalized;
    }

    private static string GetSafeExtension(
        string originalFileName,
        string contentType)
    {
        var suppliedExtension = Path.GetExtension(
                originalFileName)
            .ToLowerInvariant();

        return contentType
            .Trim()
            .ToLowerInvariant()
            switch
        {
            "image/jpeg" =>
                suppliedExtension is ".jpeg" or ".jpg"
                    ? suppliedExtension
                    : ".jpg",

            "image/png" => ".png",
            "image/webp" => ".webp",
            "application/pdf" => ".pdf",

            _ => throw new InvalidOperationException(
                "Unsupported document type.")
        };
    }
}