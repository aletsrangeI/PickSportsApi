using System.Security.Claims;
using DTO.Quiniela;
using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class QuinielasController : ControllerBase
{
    private readonly IQuinielaApplication _quinielaApplication;

    public QuinielasController(IQuinielaApplication quinielaApplication)
    {
        _quinielaApplication = quinielaApplication;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                    ?? User.FindFirst(ClaimTypes.Name)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQuinielaDto request)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _quinielaApplication.CreateAsync(userId, request);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return CreatedAtAction(nameof(GetById), new { id = response.Data.Id }, response);
    }

    [AllowAnonymous]
    [HttpGet("leagues")]
    public async Task<IActionResult> GetLeagues()
    {
        var response = await _quinielaApplication.GetActiveLeaguesAsync();
        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetUserQuinielas()
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _quinielaApplication.GetUserQuinielasAsync(userId);
        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _quinielaApplication.GetByIdAsync(id, userId);
        if (!response.isSuccess)
        {
            return NotFound(response);
        }

        return Ok(response);
    }

    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] JoinQuinielaDto request)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _quinielaApplication.JoinAsync(userId, request);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPatch("{id}/members/{memberId}/payment")]
    public async Task<IActionResult> UpdateMemberPayment(
        int id,
        int memberId,
        [FromBody] UpdateMemberPaymentDto request)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _quinielaApplication.UpdatePaymentStatusAsync(id, memberId, request.PaidFee, userId);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }
}
