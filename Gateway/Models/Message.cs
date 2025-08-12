using System.ComponentModel.DataAnnotations;

namespace Gateway.Models;

public class Message
{
    public int Id { get; set; }
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    public bool IsFromUser { get; set; }
    
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    
    [MaxLength(20)]
    public string Status { get; set; } = "sent"; // sent, delivered, read
    
    public string? MediaUrl { get; set; }
    
    [MaxLength(50)]
    public string? MediaType { get; set; }
    
    // Foreign keys
    public int ConversationId { get; set; }
    public int? MediaFileId { get; set; }
    
    // Navigation properties
    public Conversation Conversation { get; set; } = null!;
    public MediaFile? MediaFile { get; set; }
}
