using Common;
using DTO.Scoring;

namespace Interface.UseCases;

public interface IWhatsAppReportService
{
    Task<Response<WhatsAppTextReportDto>> GenerateReminderReportAsync(int quinielaId, int weekId, string? appBaseUrl = null);
    Task<Response<WhatsAppTextReportDto>> GenerateSummaryReportAsync(int quinielaId, int weekId);
    Task<Response<WhatsAppTextReportDto>> GeneratePrizePoolReportAsync(int quinielaId);
    Task<Response<WhatsAppTextReportDto>> GeneratePlayerReportAsync(int quinielaId, int weekId, int memberId);
}
