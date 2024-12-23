using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Common;
using DTO.User;
using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WebApi.Helpers;

namespace TimelyIO.Service.WebApi.Controllers;

[Authorize]
[Route("api/[controller]/[action]")]
[ApiController]
public class UsersController : Controller
{
    private readonly AppSettings _appSettings;
    private readonly IUsersApplication _usersApplication;

    public UsersController(IUsersApplication usersApplication, IOptions<AppSettings> appSettings)
    {
        _usersApplication = usersApplication;
        _appSettings = appSettings.Value;
    }

    [AllowAnonymous]
    [HttpPost]
    [ActionName("Authenticate")]
    public IActionResult Authenticate([FromBody] UserDTO usersDto)
    {
        var response = _usersApplication.Authenticate(usersDto.Email, usersDto.Password);

        if (!response.isSuccess) return BadRequest(response);

        if (response.Data == null) return NotFound(response.Message);

        response.Data.Token = BuildToken(response);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost]
    [ActionName("Register")]
    public IActionResult Register([FromBody] UserDTO usersDto)
    {
        var response = _usersApplication.Insert(usersDto);

        if (!response.isSuccess) return BadRequest(response);

        if (response.Data == null) return NotFound(response.Message);

        return Ok(response);
    }

    [HttpGet("GetAll")]
    public IActionResult GetAll()
    {
        var response = _usersApplication.GetAll();
        return Ok(response);
    }

    [HttpGet("GetById/{id}")]
    public IActionResult GetById(int id)
    {
        var response = _usersApplication.Get(id);
        return Ok(response);
    }

    [HttpPut("Update")]
    public IActionResult Update([FromBody] UserDTO usersDto)
    {
        var response = _usersApplication.Update(usersDto);
        return Ok(response);
    }

    [HttpDelete("Delete")]
    public IActionResult Delete(int id)
    {
        var response = _usersApplication.Delete(id);
        return Ok(response);
    }

    [HttpGet("GetallWithPagination")]
    public IActionResult GetallWithPagination(int page, int pageSize)
    {
        var response = _usersApplication.GetAllWithPagination(page, pageSize);
        return Ok(response);
    }

    [HttpGet("Count")]
    public IActionResult Count()
    {
        var response = _usersApplication.Count();
        return Ok(response);
    }


    private string BuildToken(Response<UserDTO> usersDTO)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_appSettings.Secret);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new Claim[]
            {
                new(ClaimTypes.Name, usersDTO.Data.Id.ToString())
            }),

            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials =
                new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Issuer = _appSettings.Issuer,
            Audience = _appSettings.Audience
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);
        return tokenString;
    }

    #region Metodos asincronos

    [AllowAnonymous]
    [HttpPost]
    [ActionName("RegisterAsync")]
    public async Task<IActionResult> RegisterAsync([FromBody] UserDTO usersDto)
    {
        var response = await _usersApplication.InsertAsync(usersDto);

        if (!response.isSuccess) return BadRequest(response);

        if (response.Data == null) return NotFound(response.Message);

        return Ok(response);
    }

    [HttpGet("GetAllAsync")]
    public async Task<IActionResult> GetAllAsync()
    {
        var response = await _usersApplication.GetAllAsync();
        return Ok(response);
    }

    [HttpGet("GetByIdAsync/{id}")]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        var response = await _usersApplication.GetAsync(id);
        return Ok(response);
    }

    [HttpPut("UpdateAsync")]
    public async Task<IActionResult> UpdateAsync([FromBody] UserDTO usersDto)
    {
        var response = await _usersApplication.UpdateAsync(usersDto);
        return Ok(response);
    }

    [HttpDelete("DeleteAsync")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        var response = await _usersApplication.DeleteAsync(id);
        return Ok(response);
    }

    [HttpGet("GetallWithPaginationAsync")]
    public async Task<IActionResult> GetallWithPaginationAsync(int page, int pageSize)
    {
        var response = await _usersApplication.GetAllWithPaginationAsync(page, pageSize);
        return Ok(response);
    }

    [HttpGet("CountAsync")]
    public async Task<IActionResult> CountAsync()
    {
        var response = await _usersApplication.CountAsync();
        return Ok(response);
    }

    #endregion
}