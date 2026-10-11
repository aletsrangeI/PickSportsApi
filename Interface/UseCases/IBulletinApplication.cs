using Common;
using DTO.Bulletin;

namespace Interface.UseCases;

public interface IBulletinApplication
{
    Task<Response<WeeklyBulletinDto>> GetBulletinAsync(int quinielaId, int? weekId);
    Task<Response<WeeklyBulletinDto>> UpdateAnnouncementAsync(int quinielaId, int weekId, int userId, UpdateAnnouncementDto dto);
    Task<Response<WeeklyBulletinDto>> RegenerateChronicleAsync(int quinielaId, int weekId, int userId);
    Task<Response<WeeklyBulletinDto>> UpdateChronicleAsync(int quinielaId, int weekId, int userId, UpdateChronicleDto dto);
}
