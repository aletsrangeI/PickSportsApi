using Common;
using DTO.Auth;

namespace Interface.UseCases;

public interface IAuthApplication
{
    Task<Response<AuthResponseDto>> RegisterAsync(RegisterRequestDto request);
    Task<Response<AuthResponseDto>> LoginAsync(LoginRequestDto request);
    Task<Response<UserProfileDto>> GetCurrentUserAsync(int userId);
}
