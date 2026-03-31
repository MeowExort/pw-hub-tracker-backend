namespace Pw.Hub.Tracker.Domain.Entities;

public class ArenaTeamMember
{
    public long TeamId { get; set; }
    public long PlayerId { get; set; }
    public string PlayerServer { get; set; }
    public long RewardMoneyInfo { get; set; }

    public ArenaTeam Team { get; set; } = null!;
    public ArenaPlayer Player { get; set; } = null!;
}
