using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using Gateway.Models.DTOs;

namespace Gateway.Services;

public interface IAzureBlobService
{
    Task<MediaUploadResponse?> UploadImageAsync(IFormFile file, int userId);
    Task<bool> DeleteImageAsync(string blobUrl);
    Task<Stream?> DownloadImageAsync(string blobUrl);
    Task<string> GetSignedUrlAsync(string blobUrl, TimeSpan expiry);
}

public class AzureBlobService : IAzureBlobService
{
    private readonly BlobServiceClient? _blobServiceClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AzureBlobService> _logger;
    private readonly string _containerName;
    private readonly string _baseUrl;

    public AzureBlobService(IConfiguration configuration, ILogger<AzureBlobService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        var connectionString = _configuration["AzureStorage:ConnectionString"];
        _containerName = _configuration["AzureStorage:ContainerName"] ?? "messenger-media";
        _baseUrl = _configuration["AzureStorage:BaseUrl"] ?? "";

        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogWarning("Azure Storage connection string not configured. Media upload will be disabled.");
            return;
        }

        _blobServiceClient = new BlobServiceClient(connectionString);
        
        // Créer le conteneur s'il n'existe pas
        _ = Task.Run(async () =>
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la création du conteneur Azure Blob");
            }
        });
    }

    public async Task<MediaUploadResponse?> UploadImageAsync(IFormFile file, int userId)
    {
        try
        {
            if (_blobServiceClient == null)
            {
                _logger.LogError("Azure Blob Service Client not initialized");
                return null;
            }

            // Validation du fichier
            if (!IsValidImageFile(file))
            {
                _logger.LogWarning("Type de fichier non supporté: {ContentType}", file.ContentType);
                return null;
            }

            // Générer un nom unique pour le fichier
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var uniqueFileName = $"{userId}/{Guid.NewGuid()}{fileExtension}";
            var thumbnailFileName = $"{userId}/thumbnails/{Guid.NewGuid()}_thumb{fileExtension}";

            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);

            // Traitement de l'image
            using var originalStream = file.OpenReadStream();
            using var image = await Image.LoadAsync(originalStream);

            var originalWidth = image.Width;
            var originalHeight = image.Height;

            // Upload de l'image originale (redimensionnée si trop grande)
            using var processedStream = new MemoryStream();
            if (image.Width > 1920 || image.Height > 1080)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(1920, 1080),
                    Mode = ResizeMode.Max
                }));
            }

            await image.SaveAsJpegAsync(processedStream, new JpegEncoder { Quality = 85 });
            processedStream.Position = 0;

            var blobClient = containerClient.GetBlobClient(uniqueFileName);
            await blobClient.UploadAsync(processedStream, new BlobHttpHeaders
            {
                ContentType = "image/jpeg"
            });

            // Créer et upload du thumbnail
            using var thumbnailStream = new MemoryStream();
            image.Mutate(x => x.Resize(300, 300));
            await image.SaveAsJpegAsync(thumbnailStream, new JpegEncoder { Quality = 80 });
            thumbnailStream.Position = 0;

            var thumbnailBlobClient = containerClient.GetBlobClient(thumbnailFileName);
            await thumbnailBlobClient.UploadAsync(thumbnailStream, new BlobHttpHeaders
            {
                ContentType = "image/jpeg"
            });

            var response = new MediaUploadResponse
            {
                FileName = file.FileName,
                BlobUrl = blobClient.Uri.ToString(),
                ThumbnailUrl = thumbnailBlobClient.Uri.ToString(),
                ContentType = "image/jpeg",
                FileSize = processedStream.Length,
                Width = originalWidth,
                Height = originalHeight,
                UploadedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Image uploadée avec succès pour l'utilisateur {UserId}: {FileName}", userId, file.FileName);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'upload de l'image pour l'utilisateur {UserId}", userId);
            return null;
        }
    }

    public async Task<bool> DeleteImageAsync(string blobUrl)
    {
        try
        {
            if (_blobServiceClient == null)
            {
                return false;
            }

            var uri = new Uri(blobUrl);
            var blobName = uri.Segments.Last();
            
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            
            var response = await blobClient.DeleteIfExistsAsync();
            
            _logger.LogInformation("Image supprimée: {BlobUrl}, Succès: {Success}", blobUrl, response.Value);
            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la suppression de l'image: {BlobUrl}", blobUrl);
            return false;
        }
    }

    public async Task<Stream?> DownloadImageAsync(string blobUrl)
    {
        try
        {
            if (_blobServiceClient == null)
            {
                return null;
            }

            var uri = new Uri(blobUrl);
            var blobName = uri.Segments.Last();
            
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            
            var response = await blobClient.DownloadStreamingAsync();
            return response.Value.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du téléchargement de l'image: {BlobUrl}", blobUrl);
            return null;
        }
    }

    public Task<string> GetSignedUrlAsync(string blobUrl, TimeSpan expiry)
    {
        try
        {
            if (_blobServiceClient == null)
            {
                return Task.FromResult(blobUrl);
            }

            var uri = new Uri(blobUrl);
            var blobName = uri.Segments.Last();
            
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            
            // Pour simplifier, on retourne l'URL directe
            // Dans un environnement de production, vous pourriez implémenter des SAS tokens
            return Task.FromResult(blobUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la génération de l'URL signée: {BlobUrl}", blobUrl);
            return Task.FromResult(blobUrl);
        }
    }

    private static bool IsValidImageFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return false;

        // Vérifier la taille (max 10MB)
        if (file.Length > 10 * 1024 * 1024)
            return false;

        // Vérifier le type MIME
        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
        return allowedTypes.Contains(file.ContentType?.ToLowerInvariant());
    }
}
