namespace Pw.Hub.Tracker.Domain.Entities;

public enum EntityType : short
{
    Team = 0,
    Player = 1
}

public class ArenaBattleStats
{
    public long EntityId { get; set; }
    public string Server { get; set; }
    public EntityType EntityType { get; set; }
    public int MatchPattern { get; set; }
    public int Score { get; set; }
    public int WinCount { get; set; }
    public int BattleCount { get; set; }
    public int WeekBattleCount { get; set; }
    public int WeekWinCount { get; set; }
    public int WeekMaxScore { get; set; }
    public int Rank { get; set; }
}
