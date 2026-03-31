namespace Pw.Hub.Tracker.Domain.Entities;

public class ArenaScoreHistory
{
    public long Id { get; set; }
    public long EntityId { get; set; }
    public string Server { get; set; }
    public EntityType EntityType { get; set; }
    public int MatchPattern { get; set; }
    public int Score { get; set; }
    public int WinCount { get; set; }
    public int BattleCount { get; set; }
    public int? MemberCount { get; set; }
    public double? NormalizedScore => MemberCount is > 0 ? (double)Score / MemberCount.Value : null;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
