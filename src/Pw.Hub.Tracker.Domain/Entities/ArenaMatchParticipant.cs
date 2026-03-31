namespace Pw.Hub.Tracker.Domain.Entities;

public class ArenaMatchParticipant
{
    public long MatchId { get; set; }
    public long TeamId { get; set; }
    public long PlayerId { get; set; }
    public string PlayerServer { get; set; }
    public int PlayerCls { get; set; }
    public int? ScoreBefore { get; set; }
    public int? ScoreAfter { get; set; }
    public bool IsWinner { get; set; }

    public ArenaMatch Match { get; set; } = null!;
    public ArenaTeam Team { get; set; } = null!;
    public ArenaPlayer Player { get; set; } = null!;
}
