using Common;
using DTO.Auth;

namespace Interface.UseCases;

public interface IAuthApplication
{
    Task<Response<AuthResponseDto>> RegisterAsync(RegisterRequestDto request);
    Task<Response<AuthResponseDto>> LoginAsync(LoginRequestDto request);
    Task<Response<UserProfileDto>> GetCurrentUserAsync(int userId);
    Task<Response<ClaimInfoDto>> GetClaimInfoAsync(string token);
    Task<Response<AuthResponseDto>> ClaimAccountAsync(ClaimAccountRequestDto request);
    Task<Response<UnclaimedQuinielaMembersDto>> GetUnclaimedMembersAsync(int? quinielaId = null);
    Task<Response<AuthResponseDto>> LinkClaimedMemberForCurrentUserAsync(int currentUserId, string token);
}
