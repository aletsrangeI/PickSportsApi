using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/[controller]")]
public class LeaguesController : ControllerBase
{
    private readonly IQuinielaApplication _quinielaApplication;

    public LeaguesController(IQuinielaApplication quinielaApplication)
    {
        _quinielaApplication = quinielaApplication;
    }

    /// <summary>GET /api/leagues — lista de ligas activas con información de su deporte.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var response = await _quinielaApplication.GetActiveLeaguesAsync();
        return Ok(response);
    }
}
