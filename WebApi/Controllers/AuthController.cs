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

    public AuthController(IAuthApplication authApplication)
    {
        _authApplication = authApplication;
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
}
