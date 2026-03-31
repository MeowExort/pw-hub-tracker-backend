namespace Pw.Hub.Tracker.Domain.Entities;

public class ArenaMatch
{
    public long Id { get; set; }
    public int MatchPattern { get; set; }
    public long? TeamAId { get; set; }
    public long? TeamBId { get; set; }
    public long? WinnerTeamId { get; set; }
    public long? LoserTeamId { get; set; }
    public int? TeamAScoreBefore { get; set; }
    public int? TeamAScoreAfter { get; set; }
    public int? TeamBScoreBefore { get; set; }
    public int? TeamBScoreAfter { get; set; }
    public long? OriginalMatchId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ArenaMatch? OriginalMatch { get; set; }
    public ArenaTeam? TeamA { get; set; }
    public ArenaTeam? TeamB { get; set; }
    public ICollection<ArenaMatchParticipant> Participants { get; set; } = [];
}
