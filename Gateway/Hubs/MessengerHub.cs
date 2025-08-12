using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Gateway.Hubs;

[Authorize]
public class MessengerHub : Hub
{
    private readonly ILogger<MessengerHub> _logger;

    public MessengerHub(ILogger<MessengerHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        if (userId != null)
        {
            // Ajouter l'utilisateur à un groupe basé sur son ID
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
            
            // Notifier les autres utilisateurs que cet utilisateur est en ligne
            await Clients.Others.SendAsync("UserOnline", userId);
            
            _logger.LogInformation("Utilisateur {UserId} connecté au hub SignalR", userId);
        }
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();
        if (userId != null)
        {
            // Retirer l'utilisateur du groupe
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
            
            // Notifier les autres utilisateurs que cet utilisateur est hors ligne
            await Clients.Others.SendAsync("UserOffline", userId);
            
            _logger.LogInformation("Utilisateur {UserId} déconnecté du hub SignalR", userId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }

    // Méthode pour rejoindre une conversation spécifique
    public async Task JoinConversation(string conversationId)
    {
        var userId = GetCurrentUserId();
        if (userId != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Conversation_{conversationId}");
            _logger.LogInformation("Utilisateur {UserId} a rejoint la conversation {ConversationId}", userId, conversationId);
        }
    }

    // Méthode pour quitter une conversation
    public async Task LeaveConversation(string conversationId)
    {
        var userId = GetCurrentUserId();
        if (userId != null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Conversation_{conversationId}");
            _logger.LogInformation("Utilisateur {UserId} a quitté la conversation {ConversationId}", userId, conversationId);
        }
    }

    // Méthode pour envoyer un indicateur de frappe
    public async Task SendTypingIndicator(string conversationId, bool isTyping)
    {
        var userId = GetCurrentUserId();
        var username = GetCurrentUsername();
        
        if (userId != null && username != null)
        {
            await Clients.GroupExcept($"Conversation_{conversationId}", Context.ConnectionId)
                .SendAsync("TypingIndicator", new
                {
                    UserId = userId,
                    Username = username,
                    ConversationId = conversationId,
                    IsTyping = isTyping
                });
        }
    }

    // Méthode pour marquer un message comme lu
    public async Task MarkMessageAsRead(string conversationId, int messageId)
    {
        var userId = GetCurrentUserId();
        
        if (userId != null)
        {
            await Clients.Group($"Conversation_{conversationId}")
                .SendAsync("MessageRead", new
                {
                    MessageId = messageId,
                    ReadByUserId = userId,
                    ReadAt = DateTime.UtcNow
                });
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private string? GetCurrentUsername()
    {
        return Context.User?.FindFirst(ClaimTypes.Name)?.Value;
    }
}

// Service pour envoyer des notifications via SignalR depuis d'autres parties de l'application
public interface IMessengerHubService
{
    Task SendNewMessageNotification(int userId, object message);
    Task SendMessageStatusUpdate(int conversationId, int messageId, string status);
    Task NotifyUserOnline(int userId);
    Task NotifyUserOffline(int userId);
}

public class MessengerHubService : IMessengerHubService
{
    private readonly IHubContext<MessengerHub> _hubContext;
    private readonly ILogger<MessengerHubService> _logger;

    public MessengerHubService(IHubContext<MessengerHub> hubContext, ILogger<MessengerHubService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendNewMessageNotification(int userId, object message)
    {
        try
        {
            await _hubContext.Clients.Group($"User_{userId}")
                .SendAsync("NewMessage", message);
            
            _logger.LogInformation("Notification de nouveau message envoyée à l'utilisateur {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'envoi de la notification de nouveau message à l'utilisateur {UserId}", userId);
        }
    }

    public async Task SendMessageStatusUpdate(int conversationId, int messageId, string status)
    {
        try
        {
            await _hubContext.Clients.Group($"Conversation_{conversationId}")
                .SendAsync("MessageStatusUpdate", new
                {
                    MessageId = messageId,
                    Status = status,
                    UpdatedAt = DateTime.UtcNow
                });
            
            _logger.LogInformation("Mise à jour du statut du message {MessageId} envoyée pour la conversation {ConversationId}", 
                messageId, conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'envoi de la mise à jour du statut du message {MessageId}", messageId);
        }
    }

    public async Task NotifyUserOnline(int userId)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("UserOnline", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la notification de connexion de l'utilisateur {UserId}", userId);
        }
    }

    public async Task NotifyUserOffline(int userId)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("UserOffline", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la notification de déconnexion de l'utilisateur {UserId}", userId);
        }
    }
}
