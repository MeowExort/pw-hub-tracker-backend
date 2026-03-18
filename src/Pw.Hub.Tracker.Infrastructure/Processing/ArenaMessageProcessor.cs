using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pw.Hub.Tracker.Domain.Entities;
using Pw.Hub.Tracker.Sync.Web.Models;
using Pw.Hub.Tracker.Infrastructure.Cache;
using Pw.Hub.Tracker.Infrastructure.Helpers;

namespace Pw.Hub.Tracker.Infrastructure.Processing;

public class ArenaMessageProcessor(
    NpgsqlDataSource dataSource,
    ArenaStateCache cache,
    ILogger<ArenaMessageProcessor> logger)
{
    public async Task ProcessAsync(ArenaEventData data)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            foreach (var teamDto in data.Teams)
            {
                await UpsertTeamAsync(connection, transaction, teamDto);
                await UpsertTeamMembersAsync(connection, transaction, teamDto);

                var teamPlayers = data.Players.Where(p => p.TeamId == teamDto.Id).ToList();
                foreach (var playerDto in teamPlayers)
                    await UpsertPlayerAsync(connection, transaction, playerDto);

                var currentMatchIds = teamDto.MatchTeamId
                    .Select(m => ArenaDecoder.DecodeMatchId(m.Data))
                    .ToHashSet();

                foreach (var battleInfo in teamDto.BattleInfo)
                {
                    var prevTeam = await cache.GetTeamSnapshotAsync(teamDto.Id, battleInfo.MatchPattern);

                    await UpsertBattleStatsAsync(connection, transaction, teamDto.Id, EntityType.Team, battleInfo);

                    if (prevTeam is not null && battleInfo.BattleCount > prevTeam.BattleCount)
                    {
                        var isWin = battleInfo.WinCount > prevTeam.WinCount;
                        var newMatchId = await DetectNewMatchIdAsync(teamDto.Id, currentMatchIds);

                        var participants = await DetectParticipantsAsync(teamPlayers, battleInfo.MatchPattern, isWin);

                        if (newMatchId.HasValue)
                        {
                            await SaveMatchAsync(connection, transaction, newMatchId.Value, teamDto.Id,
                                battleInfo.MatchPattern, isWin, prevTeam.Score, battleInfo.Score, participants);
                        }

                        await RecordScoreHistoryAsync(connection, transaction, teamDto.Id, EntityType.Team, battleInfo);
                    }
                    else if (prevTeam is null || battleInfo.Score != prevTeam.Score)
                    {
                        await RecordScoreHistoryAsync(connection, transaction, teamDto.Id, EntityType.Team, battleInfo);
                    }

                    await cache.SetTeamSnapshotAsync(teamDto.Id, battleInfo.MatchPattern,
                        new BattleSnapshot(battleInfo.Score, battleInfo.BattleCount, battleInfo.WinCount));
                }

                foreach (var playerDto in teamPlayers)
                {
                    foreach (var battleInfo in playerDto.BattleInfo)
                    {
                        var prevPlayer = await cache.GetPlayerSnapshotAsync(playerDto.Id, battleInfo.MatchPattern);

                        await UpsertBattleStatsAsync(connection, transaction, playerDto.Id, EntityType.Player, battleInfo);

                        if (prevPlayer is null || battleInfo.Score != prevPlayer.Score
                                               || battleInfo.BattleCount != prevPlayer.BattleCount)
                        {
                            await RecordScoreHistoryAsync(connection, transaction, playerDto.Id, EntityType.Player, battleInfo);
                        }

                        await cache.SetPlayerSnapshotAsync(playerDto.Id, battleInfo.MatchPattern,
                            new BattleSnapshot(battleInfo.Score, battleInfo.BattleCount, battleInfo.WinCount));
                    }
                }

                await cache.SetTeamMatchIdsAsync(teamDto.Id, currentMatchIds);
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task UpsertTeamAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ArenaTeamDto dto)
    {
        string? decodedName = null;
        if (!string.IsNullOrEmpty(dto.Name.Data))
        {
            var decoded = ArenaDecoder.DecodeTeamName(dto.Name.Data);
            if (!string.IsNullOrEmpty(decoded))
                decodedName = decoded;
        }

        const string sql = """
            INSERT INTO arena_teams (id, captain_id, zone_id, name, week_reset_timestamp, last_visite_timestamp, updated_at)
            VALUES (@Id, @CaptainId, @ZoneId, @Name, @WeekResetTimestamp, @LastVisiteTimestamp, @UpdatedAt)
            ON CONFLICT (id) DO UPDATE SET
                captain_id = EXCLUDED.captain_id,
                zone_id = EXCLUDED.zone_id,
                name = COALESCE(NULLIF(EXCLUDED.name, ''), arena_teams.name),
                week_reset_timestamp = EXCLUDED.week_reset_timestamp,
                last_visite_timestamp = EXCLUDED.last_visite_timestamp,
                updated_at = EXCLUDED.updated_at
            """;

        await connection.ExecuteAsync(sql, new
        {
            dto.Id,
            dto.CaptainId,
            dto.ZoneId,
            Name = decodedName,
            dto.WeekResetTimestamp,
            dto.LastVisiteTimestamp,
            UpdatedAt = DateTime.UtcNow
        }, transaction);
    }

    private static async Task UpsertTeamMembersAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ArenaTeamDto dto)
    {
        var incomingIds = dto.Members.Select(m => m.ArenaPlayerId).ToArray();

        if (incomingIds.Length > 0)
        {
            await connection.ExecuteAsync(
                "DELETE FROM arena_team_members WHERE team_id = @TeamId AND player_id != ALL(@PlayerIds)",
                new { TeamId = dto.Id, PlayerIds = incomingIds }, transaction);
        }
        else
        {
            await connection.ExecuteAsync(
                "DELETE FROM arena_team_members WHERE team_id = @TeamId",
                new { TeamId = dto.Id }, transaction);
        }

        const string sql = """
            INSERT INTO arena_team_members (team_id, player_id, reward_money_info)
            VALUES (@TeamId, @PlayerId, @RewardMoneyInfo)
            ON CONFLICT (team_id, player_id) DO UPDATE SET
                reward_money_info = EXCLUDED.reward_money_info
            """;

        foreach (var member in dto.Members)
        {
            await connection.ExecuteAsync(sql, new
            {
                TeamId = dto.Id,
                PlayerId = member.ArenaPlayerId,
                member.RewardMoneyInfo
            }, transaction);
        }
    }

    private static async Task UpsertPlayerAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ArenaPlayerDto dto)
    {
        const string sql = """
            INSERT INTO arena_players (id, team_id, cls, reward_money, week_reset_timestamp, last_battle_timestamp, last_visite_timestamp, updated_at)
            VALUES (@Id, @TeamId, @Cls, @RewardMoney, @WeekResetTimestamp, @LastBattleTimestamp, @LastVisiteTimestamp, @UpdatedAt)
            ON CONFLICT (id) DO UPDATE SET
                team_id = EXCLUDED.team_id,
                cls = EXCLUDED.cls,
                reward_money = EXCLUDED.reward_money,
                week_reset_timestamp = EXCLUDED.week_reset_timestamp,
                last_battle_timestamp = EXCLUDED.last_battle_timestamp,
                last_visite_timestamp = EXCLUDED.last_visite_timestamp,
                updated_at = EXCLUDED.updated_at
            """;

        await connection.ExecuteAsync(sql, new
        {
            dto.Id,
            dto.TeamId,
            dto.Cls,
            dto.RewardMoney,
            dto.WeekResetTimestamp,
            dto.LastBattleTimestamp,
            dto.LastVisiteTimestamp,
            UpdatedAt = DateTime.UtcNow
        }, transaction);
    }

    private static async Task UpsertBattleStatsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        long entityId, EntityType entityType, ArenaBattleInfoDto dto)
    {
        const string sql = """
            INSERT INTO arena_battle_stats (entity_id, entity_type, match_pattern, score, win_count, battle_count, week_battle_count, week_win_count, week_max_score, rank)
            VALUES (@EntityId, @EntityType, @MatchPattern, @Score, @WinCount, @BattleCount, @WeekBattleCount, @WeekWinCount, @WeekMaxScore, @Rank)
            ON CONFLICT (entity_id, entity_type, match_pattern) DO UPDATE SET
                score = EXCLUDED.score,
                win_count = EXCLUDED.win_count,
                battle_count = EXCLUDED.battle_count,
                week_battle_count = EXCLUDED.week_battle_count,
                week_win_count = EXCLUDED.week_win_count,
                week_max_score = EXCLUDED.week_max_score,
                rank = EXCLUDED.rank
            """;

        await connection.ExecuteAsync(sql, new
        {
            EntityId = entityId,
            EntityType = (short)entityType,
            dto.MatchPattern,
            dto.Score,
            dto.WinCount,
            dto.BattleCount,
            dto.WeekBattleCount,
            dto.WeekWinCount,
            dto.WeekMaxScore,
            dto.Rank
        }, transaction);
    }

    private async Task<long?> DetectNewMatchIdAsync(long teamId, HashSet<long> currentMatchIds)
    {
        var previousMatchIds = await cache.GetTeamMatchIdsAsync(teamId);
        var newIds = currentMatchIds.Except(previousMatchIds).ToList();
        return newIds.Count > 0 ? newIds[^1] : null;
    }

    private async Task<List<(long PlayerId, int Cls, int? ScoreBefore, int? ScoreAfter)>> DetectParticipantsAsync(
        List<ArenaPlayerDto> teamPlayers, int matchPattern, bool isWin)
    {
        var participants = new List<(long, int, int?, int?)>();

        foreach (var player in teamPlayers)
        {
            var bi = player.BattleInfo.FirstOrDefault(b => b.MatchPattern == matchPattern);
            if (bi is null) continue;

            var prev = await cache.GetPlayerSnapshotAsync(player.Id, matchPattern);
            if (prev is not null && bi.BattleCount > prev.BattleCount)
            {
                participants.Add((player.Id, player.Cls, prev.Score, bi.Score));
            }
        }

        return participants;
    }

    private async Task SaveMatchAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        long matchId, long teamId, int matchPattern,
        bool isWin, int scoreBefore, int scoreAfter,
        List<(long PlayerId, int Cls, int? ScoreBefore, int? ScoreAfter)> participants)
    {
        const string matchSql = """
            INSERT INTO arena_matches (id, match_pattern, team_a_id, team_a_score_before, team_a_score_after, winner_team_id, loser_team_id, created_at)
            VALUES (@Id, @MatchPattern, @TeamId, @ScoreBefore, @ScoreAfter, @WinnerTeamId, @LoserTeamId, @CreatedAt)
            ON CONFLICT (id) DO UPDATE SET
                team_b_id = @TeamId,
                team_b_score_before = @ScoreBefore,
                team_b_score_after = @ScoreAfter,
                winner_team_id = COALESCE(arena_matches.winner_team_id, @WinnerTeamId),
                loser_team_id = COALESCE(arena_matches.loser_team_id, @LoserTeamId)
            """;

        await connection.ExecuteAsync(matchSql, new
        {
            Id = matchId,
            MatchPattern = matchPattern,
            TeamId = teamId,
            ScoreBefore = scoreBefore,
            ScoreAfter = scoreAfter,
            WinnerTeamId = isWin ? teamId : (long?)null,
            LoserTeamId = isWin ? (long?)null : teamId,
            CreatedAt = DateTime.UtcNow
        }, transaction);

        const string participantSql = """
            INSERT INTO arena_match_participants (match_id, team_id, player_id, player_cls, score_before, score_after, is_winner)
            VALUES (@MatchId, @TeamId, @PlayerId, @PlayerCls, @ScoreBefore, @ScoreAfter, @IsWinner)
            ON CONFLICT (match_id, player_id) DO UPDATE SET
                score_before = EXCLUDED.score_before,
                score_after = EXCLUDED.score_after,
                is_winner = EXCLUDED.is_winner
            """;

        foreach (var (playerId, cls, pScoreBefore, pScoreAfter) in participants)
        {
            await connection.ExecuteAsync(participantSql, new
            {
                MatchId = matchId,
                TeamId = teamId,
                PlayerId = playerId,
                PlayerCls = cls,
                ScoreBefore = pScoreBefore,
                ScoreAfter = pScoreAfter,
                IsWinner = isWin
            }, transaction);
        }

        logger.LogInformation("Match {MatchId}: Team {TeamId} {Result} (score {Before} -> {After}, {Count} participants)",
            matchId, teamId, isWin ? "WIN" : "LOSS", scoreBefore, scoreAfter, participants.Count);
    }

    private static async Task RecordScoreHistoryAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        long entityId, EntityType entityType, ArenaBattleInfoDto dto)
    {
        const string sql = """
            INSERT INTO arena_score_history (entity_id, entity_type, match_pattern, score, win_count, battle_count, recorded_at)
            VALUES (@EntityId, @EntityType, @MatchPattern, @Score, @WinCount, @BattleCount, @RecordedAt)
            """;

        await connection.ExecuteAsync(sql, new
        {
            EntityId = entityId,
            EntityType = (short)entityType,
            dto.MatchPattern,
            dto.Score,
            dto.WinCount,
            dto.BattleCount,
            RecordedAt = DateTime.UtcNow
        }, transaction);
    }
}
