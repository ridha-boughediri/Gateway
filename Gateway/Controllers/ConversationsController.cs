using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Gateway.Services;

namespace Gateway.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly IMessageProcessingService _messageService;
    private readonly ILogger<ConversationsController> _logger;

    public ConversationsController(IMessageProcessingService messageService, ILogger<ConversationsController> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetConversations()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var conversations = await _messageService.GetConversationsAsync(userId.Value);
            return Ok(conversations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des conversations");
            return StatusCode(500, new { message = "Erreur interne du serveur" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest request)
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

            var conversation = await _messageService.GetOrCreateConversationAsync(
                userId.Value, 
                request.ContactPhoneNumber, 
                request.ContactName);

            if (conversation == null)
            {
                return BadRequest(new { message = "Impossible de créer la conversation" });
            }

            _logger.LogInformation("Conversation créée pour l'utilisateur {UserId} avec {ContactPhoneNumber}", 
                userId, request.ContactPhoneNumber);

            return Ok(new
            {
                Id = conversation.Id,
                ContactPhoneNumber = conversation.ContactPhoneNumber,
                ContactName = conversation.ContactName,
                LastMessageAt = conversation.LastMessageAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la création de la conversation");
            return StatusCode(500, new { message = "Erreur interne du serveur" });
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

public class CreateConversationRequest
{
    public string ContactPhoneNumber { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
}
