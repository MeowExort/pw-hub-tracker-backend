using System.Text.Json.Serialization;

namespace Pw.Hub.Tracker.Sync.Web.Models;

public record ArenaMessage
{
    public required string Server { get; init; }
    public required ArenaEventData Data { get; init; }
}

public record ArenaEventData
{
    public int Localsid { get; init; }
    public long Roleid { get; init; }
    public List<ArenaTeamDto> Teams { get; init; } = [];
    public List<ArenaPlayerDto> Players { get; init; } = [];
    public int Opcode { get; init; }
    public int Length { get; init; }
}

public record ArenaTeamDto
{
    public long Id { get; init; }
    [JsonPropertyName("captain_id")]
    public long CaptainId { get; init; }
    [JsonPropertyName("zone_id")]
    public int ZoneId { get; init; }
    public int Reserve0 { get; init; }
    public ArenaNameDto Name { get; init; } = new();
    public List<ArenaTeamMemberDto> Members { get; init; } = [];
    [JsonPropertyName("match_team_id")]
    public List<ArenaMatchTeamIdDto> MatchTeamId { get; init; } = [];
    [JsonPropertyName("week_reset_timestamp")]
    public long WeekResetTimestamp { get; init; }
    public int Reserve1 { get; init; }
    public int Reserve2 { get; init; }
    public int Reserve3 { get; init; }
    [JsonPropertyName("last_visite_timestamp")]
    public long LastVisiteTimestamp { get; init; }
    [JsonPropertyName("battle_info")]
    public List<ArenaBattleInfoDto> BattleInfo { get; init; } = [];
    public int Reserve4 { get; init; }
    public int Reserve5 { get; init; }
}

public record ArenaNameDto
{
    public int Size { get; init; }
    public int Length { get; init; }
}

public record ArenaTeamMemberDto
{
    [JsonPropertyName("arena_player_id")]
    public long ArenaPlayerId { get; init; }
    [JsonPropertyName("reward_money_info")]
    public long RewardMoneyInfo { get; init; }
}

public record ArenaMatchTeamIdDto
{
    public string Data { get; init; } = string.Empty;
}

public record ArenaBattleInfoDto
{
    [JsonPropertyName("match_pattern")]
    public int MatchPattern { get; init; }
    public int Score { get; init; }
    [JsonPropertyName("win_count")]
    public int WinCount { get; init; }
    [JsonPropertyName("battle_count")]
    public int BattleCount { get; init; }
    [JsonPropertyName("week_battle_count")]
    public int WeekBattleCount { get; init; }
    [JsonPropertyName("week_win_count")]
    public int WeekWinCount { get; init; }
    [JsonPropertyName("week_max_score")]
    public int WeekMaxScore { get; init; }
    public int Rank { get; init; }
    public int Reserve { get; init; }
}

public record ArenaPlayerDto
{
    public long Id { get; init; }
    [JsonPropertyName("team_id")]
    public long TeamId { get; init; }
    public int Cls { get; init; }
    [JsonPropertyName("reward_money")]
    public long RewardMoney { get; init; }
    public int Reserve2 { get; init; }
    public int Reserve3 { get; init; }
    [JsonPropertyName("week_reset_timestamp")]
    public long WeekResetTimestamp { get; init; }
    [JsonPropertyName("last_battle_timestamp")]
    public long LastBattleTimestamp { get; init; }
    [JsonPropertyName("last_visite_timestamp")]
    public long LastVisiteTimestamp { get; init; }
    [JsonPropertyName("battle_info")]
    public List<ArenaBattleInfoDto> BattleInfo { get; init; } = [];
    public int Reserve5 { get; init; }
    public int Reserve6 { get; init; }
}
