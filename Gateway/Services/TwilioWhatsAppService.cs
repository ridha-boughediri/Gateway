using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using Gateway.Models.DTOs;

namespace Gateway.Services;

public interface ITwilioWhatsAppService
{
    Task<bool> SendMessageAsync(string to, string message, string? mediaUrl = null);
    Task<bool> SendMessageAsync(SendMessageRequest request);
}

public class TwilioWhatsAppService : ITwilioWhatsAppService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TwilioWhatsAppService> _logger;
    private readonly string _whatsAppNumber;

    public TwilioWhatsAppService(IConfiguration configuration, ILogger<TwilioWhatsAppService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        var accountSid = _configuration["Twilio:AccountSid"];
        var authToken = _configuration["Twilio:AuthToken"];
        _whatsAppNumber = _configuration["Twilio:WhatsAppNumber"] ?? "whatsapp:+14155238886";

        if (string.IsNullOrEmpty(accountSid) || string.IsNullOrEmpty(authToken))
        {
            _logger.LogWarning("Twilio credentials not configured. WhatsApp functionality will be limited.");
            return;
        }

        TwilioClient.Init(accountSid, authToken);
    }

    public async Task<bool> SendMessageAsync(string to, string message, string? mediaUrl = null)
    {
        try
        {
            // S'assurer que le numéro commence par "whatsapp:"
            if (!to.StartsWith("whatsapp:"))
            {
                to = $"whatsapp:{to}";
            }

            var messageOptions = new CreateMessageOptions(new PhoneNumber(to))
            {
                From = new PhoneNumber(_whatsAppNumber),
                Body = message
            };

            // Ajouter le média si fourni
            if (!string.IsNullOrEmpty(mediaUrl))
            {
                messageOptions.MediaUrl = new List<Uri> { new Uri(mediaUrl) };
            }

            var messageResource = await MessageResource.CreateAsync(messageOptions);
            
            _logger.LogInformation("Message WhatsApp envoyé avec succès. SID: {MessageSid}", messageResource.Sid);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'envoi du message WhatsApp vers {To}", to);
            return false;
        }
    }

    public async Task<bool> SendMessageAsync(SendMessageRequest request)
    {
        return await SendMessageAsync(request.To, request.Content, request.MediaUrl);
    }
}
