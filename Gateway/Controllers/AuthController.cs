using Microsoft.AspNetCore.Mvc;
using Gateway.Models.DTOs;
using Gateway.Services;

namespace Gateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.RegisterAsync(request);
            if (result == null)
            {
                return BadRequest(new { message = "Nom d'utilisateur ou numéro de téléphone déjà utilisé" });
            }

            _logger.LogInformation("Nouvel utilisateur enregistré: {Username}", request.Username);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'enregistrement de l'utilisateur {Username}", request.Username);
            return StatusCode(500, new { message = "Erreur interne du serveur" });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.LoginAsync(request);
            if (result == null)
            {
                return Unauthorized(new { message = "Nom d'utilisateur ou mot de passe incorrect" });
            }

            _logger.LogInformation("Utilisateur connecté: {Username}", request.Username);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la connexion de l'utilisateur {Username}", request.Username);
            return StatusCode(500, new { message = "Erreur interne du serveur" });
        }
    }
}
