using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Gateway.Data;
using Gateway.Models;
using Gateway.Models.DTOs;

namespace Gateway.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContactsController : ControllerBase
{
    private readonly MessengerContext _context;
    private readonly ILogger<ContactsController> _logger;

    public ContactsController(MessengerContext context, ILogger<ContactsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetContacts()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var contacts = await _context.Contacts
                .Where(c => c.UserId == userId.Value)
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.PhoneNumber,
                    c.CreatedAt
                })
                .ToListAsync();

            return Ok(contacts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des contacts");
            return StatusCode(500, new { message = "Erreur interne du serveur" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateContact([FromBody] CreateContactRequest request)
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

            // Vérifier si le contact existe déjà
            var existingContact = await _context.Contacts
                .AnyAsync(c => c.UserId == userId.Value && c.PhoneNumber == request.PhoneNumber);

            if (existingContact)
            {
                return BadRequest(new { message = "Ce contact existe déjà" });
            }

            var contact = new Contact
            {
                UserId = userId.Value,
                Name = request.Name,
                PhoneNumber = request.PhoneNumber,
                CreatedAt = DateTime.UtcNow
            };

            _context.Contacts.Add(contact);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Contact créé pour l'utilisateur {UserId}: {ContactName}", userId, request.Name);

            return Ok(new
            {
                contact.Id,
                contact.Name,
                contact.PhoneNumber,
                contact.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la création du contact");
            return StatusCode(500, new { message = "Erreur interne du serveur" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateContact(int id, [FromBody] CreateContactRequest request)
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

            var contact = await _context.Contacts
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId.Value);

            if (contact == null)
            {
                return NotFound(new { message = "Contact non trouvé" });
            }

            contact.Name = request.Name;
            contact.PhoneNumber = request.PhoneNumber;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Contact mis à jour pour l'utilisateur {UserId}: {ContactName}", userId, request.Name);

            return Ok(new
            {
                contact.Id,
                contact.Name,
                contact.PhoneNumber,
                contact.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la mise à jour du contact {ContactId}", id);
            return StatusCode(500, new { message = "Erreur interne du serveur" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteContact(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var contact = await _context.Contacts
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId.Value);

            if (contact == null)
            {
                return NotFound(new { message = "Contact non trouvé" });
            }

            _context.Contacts.Remove(contact);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Contact supprimé pour l'utilisateur {UserId}: {ContactName}", userId, contact.Name);

            return Ok(new { message = "Contact supprimé avec succès" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la suppression du contact {ContactId}", id);
            return StatusCode(500, new { message = "Erreur interne du serveur" });
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
