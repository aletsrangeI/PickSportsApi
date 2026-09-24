using System.Security.Claims;
using DTO.Auth;
using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthApplication _authApplication;
    private readonly IAvatarStorageService _avatarStorageService;

    public AuthController(IAuthApplication authApplication, IAvatarStorageService avatarStorageService)
    {
        _authApplication = authApplication;
        _avatarStorageService = avatarStorageService;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        var response = await _authApplication.RegisterAsync(request);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var response = await _authApplication.LoginAsync(request);
        if (!response.isSuccess)
        {
            if (response.Errors != null && response.Errors.Any(e => e.PropertyName == "UNCLAIMED_ACCOUNT"))
            {
                return BadRequest(response);
            }
            return Unauthorized(response);
        }

        return Ok(response);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? User.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Identificador de usuario inválido en el token." });
        }

        var response = await _authApplication.GetCurrentUserAsync(userId);
        if (!response.isSuccess)
        {
            return NotFound(response);
        }

        return Ok(response);
    }

    [AllowAnonymous]
    [HttpGet("claim-info")]
    public async Task<IActionResult> GetClaimInfo([FromQuery] string token)
    {
        var response = await _authApplication.GetClaimInfoAsync(token);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("claim")]
    public async Task<IActionResult> ClaimAccount([FromBody] ClaimAccountRequestDto request)
    {
        var response = await _authApplication.ClaimAccountAsync(request);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [AllowAnonymous]
    [HttpGet("unclaimed-members")]
    public async Task<IActionResult> GetUnclaimedMembers([FromQuery] int? quinielaId = null)
    {
        var response = await _authApplication.GetUnclaimedMembersAsync(quinielaId);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [Authorize]
    [HttpPost("link-current-user")]
    public async Task<IActionResult> LinkCurrentUser([FromBody] LinkCurrentUserRequestDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? User.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Identificador de usuario inválido en el token." });
        }

        var response = await _authApplication.LinkClaimedMemberForCurrentUserAsync(userId, request.Token);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [Authorize]
    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAvatar([FromForm] IFormFile file)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? User.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Identificador de usuario inválido en el token." });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { isSuccess = false, message = "Debe proporcionar un archivo de imagen válido." });
        }

        await using var stream = file.OpenReadStream();
        var response = await _authApplication.UploadAvatarAsync(
            userId,
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            HttpContext.RequestAborted);

        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [Authorize]
    [HttpDelete("avatar")]
    public async Task<IActionResult> RemoveAvatar()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? User.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Identificador de usuario inválido en el token." });
        }

        var response = await _authApplication.RemoveAvatarAsync(userId, HttpContext.RequestAborted);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [AllowAnonymous]
    [HttpGet("avatar/{fileName}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public IActionResult GetAvatar(string fileName)
    {
        var filePath = _avatarStorageService.GetAvatarFilePath(fileName);
        if (filePath == null)
        {
            return NotFound(new { message = "Avatar no encontrado." });
        }

        var contentType = _avatarStorageService.GetContentType(fileName);
        return PhysicalFile(filePath, contentType);
    }
}
