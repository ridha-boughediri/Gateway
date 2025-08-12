using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Gateway.Data;
using Gateway.Models;
using Gateway.Models.DTOs;
using Gateway.Services;

namespace Gateway.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly MessengerContext _context;
    private readonly IAzureBlobService _blobService;
    private readonly ILogger<MediaController> _logger;

    public MediaController(
        MessengerContext context, 
        IAzureBlobService blobService,
        ILogger<MediaController> logger)
    {
        _context = context;
        _blobService = blobService;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadMedia(IFormFile file)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Aucun fichier fourni" });
            }

            // Upload vers Azure Blob Storage
            var uploadResult = await _blobService.UploadImageAsync(file, userId.Value);
            if (uploadResult == null)
            {
                return BadRequest(new { message = "Erreur lors de l'upload du fichier" });
            }

            // Sauvegarder les métadonnées en base de données
            var mediaFile = new MediaFile
            {
                UserId = userId.Value,
                FileName = uploadResult.FileName,
                BlobUrl = uploadResult.BlobUrl,
                ThumbnailUrl = uploadResult.ThumbnailUrl,
                ContentType = uploadResult.ContentType,
                FileSize = uploadResult.FileSize,
                Width = uploadResult.Width,
                Height = uploadResult.Height,
                UploadedAt = uploadResult.UploadedAt
            };

            _context.MediaFiles.Add(mediaFile);
            await _context.SaveChangesAsync();

            // Mettre à jour la réponse avec l'ID de la base de données
            uploadResult.Id = mediaFile.Id;

            _logger.LogInformation("Média uploadé avec succès pour l'utilisateur {UserId}: {FileName}", userId, file.FileName);
            return Ok(uploadResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'upload du média");
            return StatusCode(500, new { message = "Erreur interne du serveur" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMedia(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var mediaFile = await _context.MediaFiles
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId.Value);

            if (mediaFile == null)
            {
                return NotFound(new { message = "Fichier média non trouvé" });
            }

            var response = new MediaFileResponse
            {
                Id = mediaFile.Id,
                FileName = mediaFile.FileName,
                BlobUrl = mediaFile.BlobUrl,
                ThumbnailUrl = mediaFile.ThumbnailUrl,
                ContentType = mediaFile.ContentType,
                FileSize = mediaFile.FileSize,
                Width = mediaFile.Width,
                Height = mediaFile.Height,
                UploadedAt = mediaFile.UploadedAt
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération du média {MediaId}", id);
            return StatusCode(500, new { message = "Erreur interne du serveur" });
        }
    }

    [HttpGet("user")]
    public async Task<IActionResult> GetUserMedia([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var skip = (page - 1) * pageSize;

            var mediaFiles = await _context.MediaFiles
                .Where(m => m.UserId == userId.Value)
                .OrderByDescending(m => m.UploadedAt)
                .Skip(skip)
                .Take(pageSize)
                .Select(m => new MediaFileResponse
                {
                    Id = m.Id,
                    FileName = m.FileName,
                    BlobUrl = m.BlobUrl,
                    ThumbnailUrl = m.ThumbnailUrl,
                    ContentType = m.ContentType,
                    FileSize = m.FileSize,
                    Width = m.Width,
                    Height = m.Height,
                    UploadedAt = m.UploadedAt
                })
                .ToListAsync();

            var totalCount = await _context.MediaFiles
                .CountAsync(m => m.UserId == userId.Value);

            return Ok(new
            {
                MediaFiles = mediaFiles,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des médias de l'utilisateur");
            return StatusCode(500, new { message = "Erreur interne du serveur" });
        }
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadMedia(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var mediaFile = await _context.MediaFiles
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId.Value);

            if (mediaFile == null)
            {
                return NotFound(new { message = "Fichier média non trouvé" });
            }

            var stream = await _blobService.DownloadImageAsync(mediaFile.BlobUrl);
            if (stream == null)
            {
                return NotFound(new { message = "Fichier non disponible" });
            }

            return File(stream, mediaFile.ContentType, mediaFile.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du téléchargement du média {MediaId}", id);
            return StatusCode(500, new { message = "Erreur interne du serveur" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMedia(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var mediaFile = await _context.MediaFiles
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId.Value);

            if (mediaFile == null)
            {
                return NotFound(new { message = "Fichier média non trouvé" });
            }

            // Supprimer de Azure Blob Storage
            await _blobService.DeleteImageAsync(mediaFile.BlobUrl);
            if (!string.IsNullOrEmpty(mediaFile.ThumbnailUrl))
            {
                await _blobService.DeleteImageAsync(mediaFile.ThumbnailUrl);
            }

            // Supprimer de la base de données
            _context.MediaFiles.Remove(mediaFile);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Média supprimé avec succès: {MediaId}", id);
            return Ok(new { message = "Fichier supprimé avec succès" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la suppression du média {MediaId}", id);
            return StatusCode(500, new { message = "Erreur interne du serveur" });
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
