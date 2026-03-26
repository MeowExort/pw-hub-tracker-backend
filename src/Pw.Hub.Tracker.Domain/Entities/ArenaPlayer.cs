namespace Pw.Hub.Tracker.Domain.Entities;

public class ArenaPlayer
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long TeamId { get; set; }
    public int Cls { get; set; }
    public long RewardMoney { get; set; }
    public long WeekResetTimestamp { get; set; }
    public long LastBattleTimestamp { get; set; }
    public long LastVisiteTimestamp { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ArenaTeam Team { get; set; } = null!;
    public ICollection<ArenaBattleStats> BattleStats { get; set; } = [];
}
