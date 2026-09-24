namespace DTO.Migration;

public class MigrationPlayerVerificationDto
{
    public string Alias { get; set; } = null!;
    public int ExpectedHits { get; set; }
    public int CalculatedHits { get; set; }
    public bool IsMatch => ExpectedHits == CalculatedHits;
}

public class MigrationResultDto
{
    public int QuinielaId { get; set; }
    public string QuinielaName { get; set; } = null!;
    public string InviteCode { get; set; } = null!;
    public int TotalMembersMigrated { get; set; }
    public int TotalMatchesMigrated { get; set; }
    public int TotalPicksMigrated { get; set; }
    public int CurrentWeekNumber { get; set; }
    public bool ValidationPassed { get; set; }
    public string Message { get; set; } = null!;
    public List<MigrationPlayerVerificationDto> PlayerVerification { get; set; } = new();
    public List<string> CreatedUsernames { get; set; } = new();
}
