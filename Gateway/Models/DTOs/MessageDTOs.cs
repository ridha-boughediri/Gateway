using System.ComponentModel.DataAnnotations;

namespace Gateway.Models.DTOs;

public class SendMessageRequest
{
    [Required]
    public string To { get; set; } = string.Empty;
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    public string? MediaUrl { get; set; }
    public string? MediaType { get; set; }
}

public class MessageResponse
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsFromUser { get; set; }
    public DateTime SentAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string? MediaType { get; set; }
}

public class ConversationResponse
{
    public int Id { get; set; }
    public string ContactPhoneNumber { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public DateTime LastMessageAt { get; set; }
    public MessageResponse? LastMessage { get; set; }
    public int UnreadCount { get; set; }
}

public class CreateContactRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;
}
