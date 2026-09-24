using Domain.Common;

namespace Domain.Entities;

public class Match : BaseAuditableEntity
{
    public int WeekId { get; set; }
    public string EspnGameId { get; set; } = null!;
    public DateTime DateUtc { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public string StatusState { get; set; } = "pre"; // pre, in, post
    public string? StatusDesc { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public string? WinnerAbbr { get; set; } // HomeAbbr, AwayAbbr o 'EMPATE'
    public bool IsUpset { get; set; }
    public string? Venue { get; set; }
    public string? City { get; set; }
    public DateTime LastSyncUtc { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// Nueva fecha tentativa cuando el partido fue pospuesto.
    /// StatusState = "postponed" nunca debe evaluarse como empate.
    /// Estados válidos: "pre" | "in" | "post" | "postponed"
    /// </summary>
    public DateTime? PostponedToDate { get; set; }

    public Week Week { get; set; } = null!;
    public Team HomeTeam { get; set; } = null!;
    public Team AwayTeam { get; set; } = null!;
    public ICollection<Pick> Picks { get; set; } = new List<Pick>();
    public ICollection<PickAuditLog> AuditLogs { get; set; } = new List<PickAuditLog>();
}
