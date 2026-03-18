namespace Pw.Hub.Tracker.Domain.Entities;

public class ArenaTeam
{
    public long Id { get; set; }
    public long CaptainId { get; set; }
    public int ZoneId { get; set; }
    public string? Name { get; set; }
    public long WeekResetTimestamp { get; set; }
    public long LastVisiteTimestamp { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ArenaTeamMember> Members { get; set; } = [];
    public ICollection<ArenaBattleStats> BattleStats { get; set; } = [];
}
