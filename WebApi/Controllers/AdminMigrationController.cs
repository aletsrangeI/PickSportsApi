using System.Security.Claims;
using Common;
using DTO.Migration;
using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/migration")]
public class AdminMigrationController : ControllerBase
{
    private readonly IQuinielaMigrationService _migrationService;

    public AdminMigrationController(IQuinielaMigrationService migrationService)
    {
        _migrationService = migrationService;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                    ?? User.FindFirst(ClaimTypes.Name)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    [HttpPost("preview")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Preview([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new Response<MigrationPreviewDto>
            {
                isSuccess = false,
                Message = "Debe proporcionar un archivo válido."
            });
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx")
        {
            return BadRequest(new Response<MigrationPreviewDto>
            {
                isSuccess = false,
                Message = "Formato de archivo inválido. Solo se admiten archivos .xlsx."
            });
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new Response<MigrationPreviewDto>
            {
                isSuccess = false,
                Message = "El archivo excede el tamaño máximo permitido de 10 MB."
            });
        }

        using var stream = file.OpenReadStream();
        var response = await _migrationService.PreviewMigrationAsync(stream, HttpContext.RequestAborted);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("execute")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Execute(
        [FromForm] IFormFile file,
        [FromForm] string? quinielaName,
        [FromForm] string? adminPlayerAlias,
        [FromForm] decimal? entryFee,
        [FromForm] decimal? firstPlacePct,
        [FromForm] decimal? secondPlacePct,
        [FromForm] decimal? thirdPlacePct)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        if (file == null || file.Length == 0)
        {
            return BadRequest(new Response<MigrationResultDto>
            {
                isSuccess = false,
                Message = "Debe proporcionar un archivo XLSX válido."
            });
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx")
        {
            return BadRequest(new Response<MigrationResultDto>
            {
                isSuccess = false,
                Message = "Formato de archivo inválido. Solo se admiten archivos .xlsx."
            });
        }

        var request = new MigrationExecuteRequestDto
        {
            QuinielaName = string.IsNullOrWhiteSpace(quinielaName) ? "Liga MX Clausura 2026" : quinielaName.Trim(),
            AdminPlayerAlias = string.IsNullOrWhiteSpace(adminPlayerAlias) ? "Alex" : adminPlayerAlias.Trim(),
            EntryFee = entryFee ?? 0.00m,
            FirstPlacePct = firstPlacePct ?? 70.00m,
            SecondPlacePct = secondPlacePct ?? 20.00m,
            ThirdPlacePct = thirdPlacePct ?? 10.00m
        };

        using var stream = file.OpenReadStream();
        var response = await _migrationService.ExecuteMigrationAsync(stream, request, userId, HttpContext.RequestAborted);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }
}
