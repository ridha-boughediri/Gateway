using Gateway.Data;
using Gateway.Models;
using Gateway.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Services;

public interface IMessageProcessingService
{
    Task<MessageResponse?> SendMessageAsync(int userId, SendMessageRequest request);
    Task<MessageResponse?> SendMessageWithMediaAsync(int userId, SendMessageWithMediaRequest request);
    Task<List<ConversationResponse>> GetConversationsAsync(int userId);
    Task<List<MessageResponse>> GetMessagesAsync(int userId, int conversationId);
    Task<MessageResponse?> ProcessIncomingMessageAsync(string from, string content, string? mediaUrl = null);
    Task<Conversation?> GetOrCreateConversationAsync(int userId, string contactPhoneNumber, string contactName);
}

public class MessageProcessingService : IMessageProcessingService
{
    private readonly MessengerContext _context;
    private readonly ITwilioWhatsAppService _twilioService;
    private readonly ILogger<MessageProcessingService> _logger;

    public MessageProcessingService(
        MessengerContext context, 
        ITwilioWhatsAppService twilioService,
        ILogger<MessageProcessingService> logger)
    {
        _context = context;
        _twilioService = twilioService;
        _logger = logger;
    }

    public async Task<MessageResponse?> SendMessageAsync(int userId, SendMessageRequest request)
    {
        try
        {
            // Nettoyer le numéro de téléphone
            var cleanPhoneNumber = CleanPhoneNumber(request.To);
            
            // Obtenir ou créer la conversation
            var conversation = await GetOrCreateConversationAsync(userId, cleanPhoneNumber, "Contact");
            if (conversation == null)
            {
                return null;
            }

            // Créer le message dans la base de données
            var message = new Message
            {
                ConversationId = conversation.Id,
                Content = request.Content,
                IsFromUser = true,
                SentAt = DateTime.UtcNow,
                Status = "sending",
                MediaUrl = request.MediaUrl,
                MediaType = request.MediaType
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Envoyer via Twilio
            var success = await _twilioService.SendMessageAsync(request);
            
            // Mettre à jour le statut
            message.Status = success ? "sent" : "failed";
            conversation.LastMessageAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();

            return new MessageResponse
            {
                Id = message.Id,
                Content = message.Content,
                IsFromUser = message.IsFromUser,
                SentAt = message.SentAt,
                Status = message.Status,
                MediaUrl = message.MediaUrl,
                MediaType = message.MediaType
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'envoi du message pour l'utilisateur {UserId}", userId);
            return null;
        }
    }

    public async Task<MessageResponse?> SendMessageWithMediaAsync(int userId, SendMessageWithMediaRequest request)
    {
        try
        {
            // Nettoyer le numéro de téléphone
            var cleanPhoneNumber = CleanPhoneNumber(request.To);
            
            // Obtenir ou créer la conversation
            var conversation = await GetOrCreateConversationAsync(userId, cleanPhoneNumber, "Contact");
            if (conversation == null)
            {
                return null;
            }

            // Récupérer le fichier média si fourni
            MediaFile? mediaFile = null;
            if (request.MediaFileId.HasValue)
            {
                mediaFile = await _context.MediaFiles
                    .FirstOrDefaultAsync(m => m.Id == request.MediaFileId.Value && m.UserId == userId);
                
                if (mediaFile == null)
                {
                    _logger.LogWarning("Fichier média {MediaFileId} non trouvé pour l'utilisateur {UserId}", 
                        request.MediaFileId, userId);
                    return null;
                }
            }

            // Créer le message dans la base de données
            var message = new Message
            {
                ConversationId = conversation.Id,
                Content = request.Content,
                IsFromUser = true,
                SentAt = DateTime.UtcNow,
                Status = "sending",
                MediaFileId = request.MediaFileId,
                MediaUrl = mediaFile?.BlobUrl,
                MediaType = mediaFile?.ContentType
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Préparer la requête Twilio
            var twilioRequest = new SendMessageRequest
            {
                To = request.To,
                Content = request.Content,
                MediaUrl = mediaFile?.BlobUrl,
                MediaType = mediaFile?.ContentType
            };

            // Envoyer via Twilio
            var success = await _twilioService.SendMessageAsync(twilioRequest);
            
            // Mettre à jour le statut
            message.Status = success ? "sent" : "failed";
            conversation.LastMessageAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();

            return new MessageResponse
            {
                Id = message.Id,
                Content = message.Content,
                IsFromUser = message.IsFromUser,
                SentAt = message.SentAt,
                Status = message.Status,
                MediaUrl = message.MediaUrl,
                MediaType = message.MediaType
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'envoi du message avec média pour l'utilisateur {UserId}", userId);
            return null;
        }
    }

    public async Task<List<ConversationResponse>> GetConversationsAsync(int userId)
    {
        var conversations = await _context.Conversations
            .Where(c => c.UserId == userId)
            .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync();

        return conversations.Select(c => new ConversationResponse
        {
            Id = c.Id,
            ContactPhoneNumber = c.ContactPhoneNumber,
            ContactName = c.ContactName,
            LastMessageAt = c.LastMessageAt,
            LastMessage = c.Messages.FirstOrDefault() != null ? new MessageResponse
            {
                Id = c.Messages.First().Id,
                Content = c.Messages.First().Content,
                IsFromUser = c.Messages.First().IsFromUser,
                SentAt = c.Messages.First().SentAt,
                Status = c.Messages.First().Status,
                MediaUrl = c.Messages.First().MediaUrl,
                MediaType = c.Messages.First().MediaType
            } : null,
            UnreadCount = c.Messages.Count(m => !m.IsFromUser && m.Status != "read")
        }).ToList();
    }

    public async Task<List<MessageResponse>> GetMessagesAsync(int userId, int conversationId)
    {
        // Vérifier que la conversation appartient à l'utilisateur
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);

        if (conversation == null)
        {
            return new List<MessageResponse>();
        }

        var messages = await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        return messages.Select(m => new MessageResponse
        {
            Id = m.Id,
            Content = m.Content,
            IsFromUser = m.IsFromUser,
            SentAt = m.SentAt,
            Status = m.Status,
            MediaUrl = m.MediaUrl,
            MediaType = m.MediaType
        }).ToList();
    }

    public async Task<MessageResponse?> ProcessIncomingMessageAsync(string from, string content, string? mediaUrl = null)
    {
        try
        {
            var cleanPhoneNumber = CleanPhoneNumber(from);
            
            // Trouver l'utilisateur qui a ce contact ou créer une conversation générique
            // Pour simplifier, on va chercher le premier utilisateur qui a ce contact
            var contact = await _context.Contacts
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.PhoneNumber == cleanPhoneNumber);

            if (contact == null)
            {
                _logger.LogWarning("Message reçu d'un numéro inconnu: {PhoneNumber}", cleanPhoneNumber);
                return null;
            }

            // Obtenir ou créer la conversation
            var conversation = await GetOrCreateConversationAsync(contact.UserId, cleanPhoneNumber, contact.Name);
            if (conversation == null)
            {
                return null;
            }

            // Créer le message
            var message = new Message
            {
                ConversationId = conversation.Id,
                Content = content,
                IsFromUser = false,
                SentAt = DateTime.UtcNow,
                Status = "delivered",
                MediaUrl = mediaUrl
            };

            _context.Messages.Add(message);
            conversation.LastMessageAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();

            return new MessageResponse
            {
                Id = message.Id,
                Content = message.Content,
                IsFromUser = message.IsFromUser,
                SentAt = message.SentAt,
                Status = message.Status,
                MediaUrl = message.MediaUrl,
                MediaType = message.MediaType
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du traitement du message entrant de {From}", from);
            return null;
        }
    }

    public async Task<Conversation?> GetOrCreateConversationAsync(int userId, string contactPhoneNumber, string contactName)
    {
        var cleanPhoneNumber = CleanPhoneNumber(contactPhoneNumber);
        
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ContactPhoneNumber == cleanPhoneNumber);

        if (conversation == null)
        {
            conversation = new Conversation
            {
                UserId = userId,
                ContactPhoneNumber = cleanPhoneNumber,
                ContactName = contactName,
                CreatedAt = DateTime.UtcNow,
                LastMessageAt = DateTime.UtcNow
            };

            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();
        }

        return conversation;
    }

    private static string CleanPhoneNumber(string phoneNumber)
    {
        // Supprimer le préfixe "whatsapp:" si présent
        if (phoneNumber.StartsWith("whatsapp:"))
        {
            phoneNumber = phoneNumber.Substring(9);
        }

        // Supprimer tous les caractères non numériques sauf le +
        return phoneNumber.Trim();
    }
}
