using Microsoft.AspNetCore.Mvc;
using Gateway.Services;

namespace Gateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly IMessageProcessingService _messageService;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(IMessageProcessingService messageService, ILogger<WebhookController> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    [HttpPost("twilio")]
    public async Task<IActionResult> TwilioWebhook()
    {
        try
        {
            // Lire les données du webhook Twilio
            var form = await Request.ReadFormAsync();
            
            var from = form["From"].ToString();
            var body = form["Body"].ToString();
            var mediaUrl = form["MediaUrl0"].ToString();
            var messageStatus = form["MessageStatus"].ToString();
            var messageSid = form["MessageSid"].ToString();

            _logger.LogInformation("Webhook Twilio reçu - From: {From}, Body: {Body}, Status: {Status}, SID: {SID}", 
                from, body, messageStatus, messageSid);

            // Si c'est un message entrant (pas un statut de livraison)
            if (!string.IsNullOrEmpty(body) && !string.IsNullOrEmpty(from))
            {
                var result = await _messageService.ProcessIncomingMessageAsync(from, body, mediaUrl);
                if (result != null)
                {
                    _logger.LogInformation("Message entrant traité avec succès pour {From}", from);
                }
                else
                {
                    _logger.LogWarning("Impossible de traiter le message entrant de {From}", from);
                }
            }

            // Répondre à Twilio avec un statut 200
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du traitement du webhook Twilio");
            return StatusCode(500);
        }
    }

    [HttpGet("test")]
    public IActionResult TestWebhook()
    {
        return Ok(new { message = "Webhook endpoint is working", timestamp = DateTime.UtcNow });
    }
}
