using System.ComponentModel.DataAnnotations;

namespace Gateway.Models.DTOs;

public class MediaUploadResponse
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class MediaFileResponse
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class SendMessageWithMediaRequest
{
    [Required]
    public string To { get; set; } = string.Empty;
    
    public string Content { get; set; } = string.Empty;
    
    public int? MediaFileId { get; set; }
}
