using System.ComponentModel.DataAnnotations;

namespace Gateway.Models;

public class User
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;
    
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public List<Contact> Contacts { get; set; } = new();
    public List<Conversation> Conversations { get; set; } = new();
    public List<MediaFile> MediaFiles { get; set; } = new();
}
