using System.ComponentModel.DataAnnotations;

namespace Gateway.Models;

public class Conversation
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string ContactPhoneNumber { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string ContactName { get; set; } = string.Empty;
    
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Foreign key
    public int UserId { get; set; }
    
    // Navigation properties
    public User User { get; set; } = null!;
    public List<Message> Messages { get; set; } = new();
}
