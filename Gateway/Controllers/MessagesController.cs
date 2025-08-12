using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Gateway.Models.DTOs;
using Gateway.Services;

namespace Gateway.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageProcessingService _messageService;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(IMessageProcessingService messageService, ILogger<MessagesController> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var result = await _messageService.SendMessageAsync(userId.Value, request);
            if (result == null)
            {
                return BadRequest(new { message = "Impossible d'envoyer le message" });
            }

            _logger.LogInformation("Message envoyé par l'utilisateur {UserId} vers {To}", userId, request.To);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'envoi du message");
            return StatusCode(500, new { message = "Erreur interne du serveur" });
        }
    }

    [HttpPost("send-with-media")]
    public async Task<IActionResult> SendMessageWithMedia([FromBody] SendMessageWithMediaRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var result = await _messageService.SendMessageWithMediaAsync(userId.Value, request);
            if (result == null)
            {
                return BadRequest(new { message = "Impossible d'envoyer le message avec média" });
            }

            _logger.LogInformation("Message avec média envoyé par l'utilisateur {UserId} vers {To}", userId, request.To);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'envoi du message avec média");
            return StatusCode(500, new { message = "Erreur interne du serveur" });
        }
    }

    [HttpGet("conversations/{conversationId}")]
    public async Task<IActionResult> GetMessages(int conversationId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var messages = await _messageService.GetMessagesAsync(userId.Value, conversationId);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des messages pour la conversation {ConversationId}", conversationId);
            return StatusCode(500, new { message = "Erreur interne du serveur" });
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
