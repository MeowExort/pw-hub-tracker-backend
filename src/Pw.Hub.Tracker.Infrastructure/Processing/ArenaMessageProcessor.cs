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
    private string GetServer(int zoneId)
    {
        switch (zoneId)
        {
            case 2: return "centaur";
            case 3: return "alkor";
            case 5: return "mizar";
            case 29: return "capella";
        }

        return "";
    }
    
    public async Task ProcessAsync(ArenaEventData data)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            foreach (var teamDto in data.Teams)
            {
                await UpsertTeamAsync(connection, transaction, teamDto);

                var teamPlayers = data.Players.Where(p => p.TeamId == teamDto.Id).ToList();
                foreach (var playerDto in teamPlayers)
                    await UpsertPlayerAsync(connection, transaction, playerDto, GetServer(teamDto.ZoneId));

                await UpsertTeamMembersAsync(connection, transaction, teamDto, GetServer(teamDto.ZoneId));

                // Шаг 1: Определяем lastBattleTimestamp — МАКСИМАЛЬНЫЙ среди всех игроков команды
                var battleTimestamp = teamPlayers
                    .Where(p => p.LastBattleTimestamp > 0)
                    .Select(p => p.LastBattleTimestamp)
                    .DefaultIfEmpty(0)
                    .Max();

                foreach (var battleInfo in teamDto.BattleInfo)
                {
                    var prevTeam = await cache.GetTeamSnapshotAsync(teamDto.Id, battleInfo.MatchPattern);

                    if (prevTeam is null)
                    {
                        var dbStats = await connection.QueryFirstOrDefaultAsync<(int Score, int BattleCount, int WinCount)?>("SELECT \"Score\", \"BattleCount\", \"WinCount\" FROM arena_battle_stats WHERE \"EntityId\" = @EntityId AND \"Server\" = @Server AND \"EntityType\" = @EntityType AND \"MatchPattern\" = @MatchPattern",
                            new { EntityId = teamDto.Id, Server = GetServer(teamDto.ZoneId), EntityType = (short)EntityType.Team, battleInfo.MatchPattern }, transaction);
                        if (dbStats.HasValue)
                            prevTeam = new BattleSnapshot(dbStats.Value.Score, dbStats.Value.BattleCount, dbStats.Value.WinCount);
                    }

                    await UpsertBattleStatsAsync(connection, transaction, teamDto.Id, EntityType.Team, battleInfo, GetServer(teamDto.ZoneId));

                    if (prevTeam is not null && battleInfo.BattleCount > prevTeam.BattleCount)
                    {
                        var isWin = battleInfo.WinCount > prevTeam.WinCount;
                        var participants = await DetectParticipantsAsync(connection, transaction, teamPlayers, battleInfo.MatchPattern, isWin, GetServer(teamDto.ZoneId));

                        if (battleTimestamp > 0)
                        {
                            var matchId = GenerateMatchId(battleTimestamp, battleInfo.MatchPattern);
                            await SaveMatchAsync(connection, transaction, matchId, teamDto.Id,
                                battleInfo.MatchPattern, isWin, prevTeam.Score, battleInfo.Score, participants, GetServer(teamDto.ZoneId));
                        }

                        await RecordScoreHistoryAsync(connection, transaction, teamDto.Id, EntityType.Team, battleInfo, teamDto.Members.Count);
                    }
                    else if (prevTeam is null || battleInfo.Score != prevTeam.Score)
                    {
                        await RecordScoreHistoryAsync(connection, transaction, teamDto.Id, EntityType.Team, battleInfo, teamDto.Members.Count);
                    }

                    await cache.SetTeamSnapshotAsync(teamDto.Id, battleInfo.MatchPattern,
                        new BattleSnapshot(battleInfo.Score, battleInfo.BattleCount, battleInfo.WinCount));
                }

                foreach (var playerDto in teamPlayers)
                {
                    foreach (var battleInfo in playerDto.BattleInfo)
                    {
                        var prevPlayer = await cache.GetPlayerSnapshotAsync(playerDto.Id, battleInfo.MatchPattern);

                        if (prevPlayer is null)
                        {
                            var dbStats = await connection.QueryFirstOrDefaultAsync<(int Score, int BattleCount, int WinCount)?>("SELECT \"Score\", \"BattleCount\", \"WinCount\" FROM arena_battle_stats WHERE \"EntityId\" = @EntityId AND \"Server\" = @Server AND \"EntityType\" = @EntityType AND \"MatchPattern\" = @MatchPattern",
                                new { EntityId = playerDto.Id, Server = GetServer(teamDto.ZoneId), EntityType = (short)EntityType.Player, battleInfo.MatchPattern }, transaction);
                            if (dbStats.HasValue)
                                prevPlayer = new BattleSnapshot(dbStats.Value.Score, dbStats.Value.BattleCount, dbStats.Value.WinCount);
                        }

                        await UpsertBattleStatsAsync(connection, transaction, playerDto.Id, EntityType.Player, battleInfo, GetServer(teamDto.ZoneId));

                        if (prevPlayer is null || battleInfo.Score != prevPlayer.Score
                                               || battleInfo.BattleCount != prevPlayer.BattleCount)
                        {
                            await RecordScoreHistoryAsync(connection, transaction, playerDto.Id, EntityType.Player, battleInfo);
                        }

                        await cache.SetPlayerSnapshotAsync(playerDto.Id, battleInfo.MatchPattern,
                            new BattleSnapshot(battleInfo.Score, battleInfo.BattleCount, battleInfo.WinCount));
                    }
                }

                // Обновить кэш timestamp
                if (battleTimestamp > 0)
                    await cache.SetTeamLastBattleTimestampAsync(teamDto.Id, battleTimestamp);
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
            INSERT INTO arena_teams ("Id", "CaptainId", "ZoneId", "Name", "WeekResetTimestamp", "LastVisiteTimestamp", "UpdatedAt")
            VALUES (@Id, @CaptainId, @ZoneId, @Name, @WeekResetTimestamp, @LastVisiteTimestamp, @UpdatedAt)
            ON CONFLICT ("Id") DO UPDATE SET
                "CaptainId" = EXCLUDED."CaptainId",
                "ZoneId" = EXCLUDED."ZoneId",
                "Name" = COALESCE(NULLIF(EXCLUDED."Name", ''), arena_teams."Name"),
                "WeekResetTimestamp" = EXCLUDED."WeekResetTimestamp",
                "LastVisiteTimestamp" = EXCLUDED."LastVisiteTimestamp",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
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

    private static async Task UpsertTeamMembersAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ArenaTeamDto dto, string server)
    {
        var incomingIds = dto.Members.Select(m => m.ArenaPlayerId).ToArray();

        if (incomingIds.Length > 0)
        {
            await connection.ExecuteAsync(
                "DELETE FROM arena_team_members WHERE \"TeamId\" = @TeamId AND \"PlayerServer\" = @PlayerServer AND \"PlayerId\" != ALL(@PlayerIds)",
                new { TeamId = dto.Id, PlayerServer = server, PlayerIds = incomingIds }, transaction);
        }
        else
        {
            await connection.ExecuteAsync(
                "DELETE FROM arena_team_members WHERE \"TeamId\" = @TeamId AND \"PlayerServer\" = @PlayerServer",
                new { TeamId = dto.Id, PlayerServer = server }, transaction);
        }

        const string sql = """
            INSERT INTO arena_team_members ("TeamId", "PlayerId", "PlayerServer", "RewardMoneyInfo")
            VALUES (@TeamId, @PlayerId, @PlayerServer, @RewardMoneyInfo)
            ON CONFLICT ("TeamId", "PlayerId", "PlayerServer") DO UPDATE SET
                "RewardMoneyInfo" = EXCLUDED."RewardMoneyInfo"
            """;

        foreach (var member in dto.Members)
        {
            await connection.ExecuteAsync(sql, new
            {
                TeamId = dto.Id,
                PlayerId = member.ArenaPlayerId,
                PlayerServer = server,
                member.RewardMoneyInfo
            }, transaction);
        }
    }

    private static async Task UpsertPlayerAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ArenaPlayerDto dto, string server)
    {
        // Upsert player base info (Cls) into players table
        const string playerSql = """
            INSERT INTO players ("Id", "Server", "Name", "Cls", "Gender", "UpdatedAt")
            VALUES (@Id, @Server, '', @Cls, 0, @UpdatedAt)
            ON CONFLICT ("Id", "Server") DO UPDATE SET
                "Cls" = EXCLUDED."Cls",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            """;

        await connection.ExecuteAsync(playerSql, new
        {
            dto.Id,
            server,
            dto.Cls,
            UpdatedAt = DateTime.UtcNow
        }, transaction);

        // Upsert arena-specific info into arena_players table
        const string sql = """
            INSERT INTO arena_players ("Id", "PlayerServer", "TeamId", "RewardMoney", "WeekResetTimestamp", "LastBattleTimestamp", "LastVisiteTimestamp", "UpdatedAt")
            VALUES (@Id, @Server, @TeamId, @RewardMoney, @WeekResetTimestamp, @LastBattleTimestamp, @LastVisiteTimestamp, @UpdatedAt)
            ON CONFLICT ("Id", "PlayerServer") DO UPDATE SET
                "TeamId" = EXCLUDED."TeamId",
                "RewardMoney" = EXCLUDED."RewardMoney",
                "WeekResetTimestamp" = EXCLUDED."WeekResetTimestamp",
                "LastBattleTimestamp" = EXCLUDED."LastBattleTimestamp",
                "LastVisiteTimestamp" = EXCLUDED."LastVisiteTimestamp",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            """;

        await connection.ExecuteAsync(sql, new
        {
            dto.Id,
            server,
            dto.TeamId,
            dto.RewardMoney,
            dto.WeekResetTimestamp,
            dto.LastBattleTimestamp,
            dto.LastVisiteTimestamp,
            UpdatedAt = DateTime.UtcNow
        }, transaction);
    }

    private static async Task UpsertBattleStatsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        long entityId, EntityType entityType, ArenaBattleInfoDto dto, string server)
    {
        const string sql = """
            INSERT INTO arena_battle_stats ("EntityId", "Server", "EntityType", "MatchPattern", "Score", "WinCount", "BattleCount", "WeekBattleCount", "WeekWinCount", "WeekMaxScore", "Rank", "ArenaTeamId", "ArenaPlayerId")
            VALUES (@EntityId, @Server, @EntityType, @MatchPattern, @Score, @WinCount, @BattleCount, @WeekBattleCount, @WeekWinCount, @WeekMaxScore, @Rank, @ArenaTeamId, @ArenaPlayerId)
            ON CONFLICT ("EntityId", "Server", "EntityType", "MatchPattern") DO UPDATE SET
                "Score" = EXCLUDED."Score",
                "WinCount" = EXCLUDED."WinCount",
                "BattleCount" = EXCLUDED."BattleCount",
                "WeekBattleCount" = EXCLUDED."WeekBattleCount",
                "WeekWinCount" = EXCLUDED."WeekWinCount",
                "WeekMaxScore" = EXCLUDED."WeekMaxScore",
                "Rank" = EXCLUDED."Rank",
                "ArenaTeamId" = COALESCE(EXCLUDED."ArenaTeamId", arena_battle_stats."ArenaTeamId"),
                "ArenaPlayerId" = COALESCE(EXCLUDED."ArenaPlayerId", arena_battle_stats."ArenaPlayerId")
            """;

        await connection.ExecuteAsync(sql, new
        {
            EntityId = entityId,
            Server = server,
            EntityType = (short)entityType,
            dto.MatchPattern,
            dto.Score,
            dto.WinCount,
            dto.BattleCount,
            dto.WeekBattleCount,
            dto.WeekWinCount,
            dto.WeekMaxScore,
            dto.Rank,
            ArenaTeamId = entityType == EntityType.Team ? (long?)entityId : null,
            ArenaPlayerId = entityType == EntityType.Player ? (long?)entityId : null
        }, transaction);
    }

    private static long GenerateMatchId(long battleTimestamp, int matchPattern)
    {
        unchecked
        {
            var hash = 17L;
            hash = hash * 31 + battleTimestamp;
            hash = hash * 31 + matchPattern;
            return hash;
        }
    }

    private static long GenerateAlternativeMatchId(long originalMatchId, long teamId)
    {
        unchecked
        {
            return originalMatchId * 31 + teamId;
        }
    }

    private async Task<List<(long PlayerId, int Cls, int? ScoreBefore, int? ScoreAfter)>> DetectParticipantsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        List<ArenaPlayerDto> teamPlayers, int matchPattern, bool isWin, string server)
    {
        var participants = new List<(long, int, int?, int?)>();

        foreach (var player in teamPlayers)
        {
            var bi = player.BattleInfo.FirstOrDefault(b => b.MatchPattern == matchPattern);
            if (bi is null) continue;

            var prev = await cache.GetPlayerSnapshotAsync(player.Id, matchPattern);
            if (prev is null)
            {
                var dbStats = await connection.QueryFirstOrDefaultAsync<(int Score, int BattleCount, int WinCount)?>("SELECT \"Score\", \"BattleCount\", \"WinCount\" FROM arena_battle_stats WHERE \"EntityId\" = @EntityId AND \"Server\" = @Server AND \"EntityType\" = @EntityType AND \"MatchPattern\" = @MatchPattern",
                    new { EntityId = player.Id, Server = server, EntityType = (short)EntityType.Player, MatchPattern = matchPattern }, transaction);
                if (dbStats.HasValue)
                    prev = new BattleSnapshot(dbStats.Value.Score, dbStats.Value.BattleCount, dbStats.Value.WinCount);
            }
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
        List<(long PlayerId, int Cls, int? ScoreBefore, int? ScoreAfter)> participants, string server,
        long? originalMatchId = null)
    {
        // Проверяем существующий матч
        var existing = await connection.QueryFirstOrDefaultAsync<(long? TeamAId, long? TeamBId, long? WinnerTeamId, long? LoserTeamId)>("""
            SELECT "TeamAId", "TeamBId", "WinnerTeamId", "LoserTeamId" FROM arena_matches WHERE "Id" = @Id
            """, new { Id = matchId }, transaction);

        if (existing.TeamAId.HasValue && existing.TeamBId.HasValue)
        {
            if (existing.TeamAId == teamId || existing.TeamBId == teamId)
                return; // Повторное сообщение от уже записанной команды — пропускаем

            // Третья+ команда — проверяем конфликт слота
            var slotConflict = (isWin && existing.WinnerTeamId.HasValue)
                            || (!isWin && existing.LoserTeamId.HasValue);

            if (slotConflict)
            {
                var newMatchId = GenerateAlternativeMatchId(matchId, teamId);
                await SaveMatchAsync(connection, transaction, newMatchId, teamId,
                    matchPattern, isWin, scoreBefore, scoreAfter, participants, server,
                    originalMatchId: originalMatchId ?? matchId);
                return;
            }

            logger.LogWarning("Match {MatchId}: unexpected free slot for team {TeamId}", matchId, teamId);
            return;
        }

        if (!existing.TeamAId.HasValue)
        {
            // Первая команда — INSERT
            const string insertSql = """
                INSERT INTO arena_matches ("Id", "MatchPattern", "TeamAId", "TeamAScoreBefore", "TeamAScoreAfter",
                    "WinnerTeamId", "LoserTeamId", "OriginalMatchId", "CreatedAt")
                VALUES (@Id, @MatchPattern, @TeamId, @ScoreBefore, @ScoreAfter,
                    @WinnerTeamId, @LoserTeamId, @OriginalMatchId, @CreatedAt)
                ON CONFLICT ("Id") DO NOTHING
                """;

            var inserted = await connection.ExecuteAsync(insertSql, new
            {
                Id = matchId,
                MatchPattern = matchPattern,
                TeamId = teamId,
                ScoreBefore = scoreBefore,
                ScoreAfter = scoreAfter,
                WinnerTeamId = isWin ? teamId : (long?)null,
                LoserTeamId = isWin ? (long?)null : teamId,
                OriginalMatchId = originalMatchId,
                CreatedAt = DateTime.UtcNow
            }, transaction);

            if (inserted == 0)
            {
                // Кто-то уже вставил — перечитываем и проверяем конфликт слота
                var current = await connection.QueryFirstOrDefaultAsync<(long? WinnerTeamId, long? LoserTeamId)>("""
                    SELECT "WinnerTeamId", "LoserTeamId" FROM arena_matches WHERE "Id" = @Id
                    """, new { Id = matchId }, transaction);

                var slotConflict = (isWin && current.WinnerTeamId.HasValue)
                                || (!isWin && current.LoserTeamId.HasValue);
                if (slotConflict)
                {
                    var newMatchId = GenerateAlternativeMatchId(matchId, teamId);
                    await SaveMatchAsync(connection, transaction, newMatchId, teamId,
                        matchPattern, isWin, scoreBefore, scoreAfter, participants, server,
                        originalMatchId: originalMatchId ?? matchId);
                    return;
                }

                await UpdateAsTeamB(connection, transaction, matchId, teamId, scoreBefore, scoreAfter, isWin);
            }
        }
        else
        {
            if (existing.TeamAId == teamId)
            {
                // Повторное сообщение от TeamA — пропускаем
                return;
            }

            // Вторая команда — проверяем конфликт слота перед UPDATE
            var slotConflict2 = (isWin && existing.WinnerTeamId.HasValue)
                             || (!isWin && existing.LoserTeamId.HasValue);
            if (slotConflict2)
            {
                var newMatchId = GenerateAlternativeMatchId(matchId, teamId);
                await SaveMatchAsync(connection, transaction, newMatchId, teamId,
                    matchPattern, isWin, scoreBefore, scoreAfter, participants, server,
                    originalMatchId: originalMatchId ?? matchId);
                return;
            }

            await UpdateAsTeamB(connection, transaction, matchId, teamId, scoreBefore, scoreAfter, isWin);
        }

        // Сохраняем участников
        const string participantSql = """
            INSERT INTO arena_match_participants ("MatchId", "TeamId", "PlayerId", "PlayerServer", "PlayerCls", "ScoreBefore", "ScoreAfter", "IsWinner")
            VALUES (@MatchId, @TeamId, @PlayerId, @PlayerServer, @PlayerCls, @ScoreBefore, @ScoreAfter, @IsWinner)
            ON CONFLICT ("MatchId", "PlayerId") DO UPDATE SET
                "ScoreBefore" = EXCLUDED."ScoreBefore",
                "ScoreAfter" = EXCLUDED."ScoreAfter",
                "IsWinner" = EXCLUDED."IsWinner"
            """;

        foreach (var (playerId, cls, pScoreBefore, pScoreAfter) in participants)
        {
            await connection.ExecuteAsync(participantSql, new
            {
                MatchId = matchId,
                TeamId = teamId,
                PlayerId = playerId,
                PlayerServer = server,
                PlayerCls = cls,
                ScoreBefore = pScoreBefore,
                ScoreAfter = pScoreAfter,
                IsWinner = isWin
            }, transaction);
        }

        logger.LogInformation("Match {MatchId}: Team {TeamId} {Result} (score {Before} -> {After}, {Count} participants)",
            matchId, teamId, isWin ? "WIN" : "LOSS", scoreBefore, scoreAfter, participants.Count);
    }

    private static async Task UpdateAsTeamB(NpgsqlConnection connection, NpgsqlTransaction transaction,
        long matchId, long teamId, int scoreBefore, int scoreAfter, bool isWin)
    {
        const string updateSql = """
            UPDATE arena_matches SET
                "TeamBId" = @TeamId,
                "TeamBScoreBefore" = @ScoreBefore,
                "TeamBScoreAfter" = @ScoreAfter,
                "WinnerTeamId" = COALESCE("WinnerTeamId", @WinnerTeamId),
                "LoserTeamId" = COALESCE("LoserTeamId", @LoserTeamId)
            WHERE "Id" = @Id AND "TeamBId" IS NULL
            """;

        await connection.ExecuteAsync(updateSql, new
        {
            Id = matchId,
            TeamId = teamId,
            ScoreBefore = scoreBefore,
            ScoreAfter = scoreAfter,
            WinnerTeamId = isWin ? teamId : (long?)null,
            LoserTeamId = isWin ? (long?)null : teamId
        }, transaction);
    }

    private static async Task RecordScoreHistoryAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        long entityId, EntityType entityType, ArenaBattleInfoDto dto, int? memberCount = null)
    {
        const string sql = """
            INSERT INTO arena_score_history ("EntityId", "EntityType", "MatchPattern", "Score", "WinCount", "BattleCount", "MemberCount", "RecordedAt")
            VALUES (@EntityId, @EntityType, @MatchPattern, @Score, @WinCount, @BattleCount, @MemberCount, @RecordedAt)
            """;

        await connection.ExecuteAsync(sql, new
        {
            EntityId = entityId,
            EntityType = (short)entityType,
            dto.MatchPattern,
            dto.Score,
            dto.WinCount,
            dto.BattleCount,
            MemberCount = memberCount,
            RecordedAt = DateTime.UtcNow
        }, transaction);
    }
}
