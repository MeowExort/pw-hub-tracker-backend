using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pw.Hub.Tracker.Domain.Entities;
using Pw.Hub.Tracker.Sync.Web.Models;
using Pw.Hub.Tracker.Infrastructure.Cache;
using Pw.Hub.Tracker.Infrastructure.Data;
using Pw.Hub.Tracker.Infrastructure.Helpers;

namespace Pw.Hub.Tracker.Infrastructure.Processing;

public class ArenaMessageProcessor(
    TrackerDbContext db,
    ArenaStateCache cache,
    ILogger<ArenaMessageProcessor> logger)
{
    public async Task ProcessAsync(ArenaEventData data)
    {
        foreach (var teamDto in data.Teams)
        {
            await UpsertTeamAsync(teamDto);
            await UpsertTeamMembersAsync(teamDto);

            var teamPlayers = data.Players.Where(p => p.TeamId == teamDto.Id).ToList();
            foreach (var playerDto in teamPlayers)
                await UpsertPlayerAsync(playerDto);

            var currentMatchIds = teamDto.MatchTeamId
                .Select(m => ArenaDecoder.DecodeMatchId(m.Data))
                .ToHashSet();

            foreach (var battleInfo in teamDto.BattleInfo)
            {
                var prevTeam = await cache.GetTeamSnapshotAsync(teamDto.Id, battleInfo.MatchPattern);

                await UpsertBattleStatsAsync(teamDto.Id, EntityType.Team, battleInfo);

                if (prevTeam is not null && battleInfo.BattleCount > prevTeam.BattleCount)
                {
                    var isWin = battleInfo.WinCount > prevTeam.WinCount;
                    var newMatchId = await DetectNewMatchIdAsync(teamDto.Id, currentMatchIds);

                    var participants = await DetectParticipantsAsync(teamPlayers, battleInfo.MatchPattern, isWin);

                    if (newMatchId.HasValue)
                    {
                        await SaveMatchAsync(newMatchId.Value, teamDto.Id, battleInfo.MatchPattern,
                            isWin, prevTeam.Score, battleInfo.Score, participants);
                    }

                    await RecordScoreHistoryAsync(teamDto.Id, EntityType.Team, battleInfo);
                }
                else if (prevTeam is null || battleInfo.Score != prevTeam.Score)
                {
                    await RecordScoreHistoryAsync(teamDto.Id, EntityType.Team, battleInfo);
                }

                await cache.SetTeamSnapshotAsync(teamDto.Id, battleInfo.MatchPattern,
                    new BattleSnapshot(battleInfo.Score, battleInfo.BattleCount, battleInfo.WinCount));
            }

            foreach (var playerDto in teamPlayers)
            {
                foreach (var battleInfo in playerDto.BattleInfo)
                {
                    var prevPlayer = await cache.GetPlayerSnapshotAsync(playerDto.Id, battleInfo.MatchPattern);

                    await UpsertBattleStatsAsync(playerDto.Id, EntityType.Player, battleInfo);

                    if (prevPlayer is null || battleInfo.Score != prevPlayer.Score
                                           || battleInfo.BattleCount != prevPlayer.BattleCount)
                    {
                        await RecordScoreHistoryAsync(playerDto.Id, EntityType.Player, battleInfo);
                    }

                    await cache.SetPlayerSnapshotAsync(playerDto.Id, battleInfo.MatchPattern,
                        new BattleSnapshot(battleInfo.Score, battleInfo.BattleCount, battleInfo.WinCount));
                }
            }

            await cache.SetTeamMatchIdsAsync(teamDto.Id, currentMatchIds);
        }

        await db.SaveChangesAsync();
    }

    private async Task UpsertTeamAsync(ArenaTeamDto dto)
    {
        var team = await db.ArenaTeams.FindAsync(dto.Id);
        if (team is null)
        {
            team = new ArenaTeam { Id = dto.Id };
            db.ArenaTeams.Add(team);
        }

        team.CaptainId = dto.CaptainId;
        team.ZoneId = dto.ZoneId;
        team.WeekResetTimestamp = dto.WeekResetTimestamp;
        team.LastVisiteTimestamp = dto.LastVisiteTimestamp;
        team.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(dto.Name.Data))
        {
            var decoded = ArenaDecoder.DecodeTeamName(dto.Name.Data);
            if (!string.IsNullOrEmpty(decoded))
                team.Name = decoded;
        }
    }

    private async Task UpsertTeamMembersAsync(ArenaTeamDto dto)
    {
        var existing = await db.ArenaTeamMembers
            .Where(m => m.TeamId == dto.Id)
            .ToListAsync();

        var incomingIds = dto.Members.Select(m => m.ArenaPlayerId).ToHashSet();
        var toRemove = existing.Where(e => !incomingIds.Contains(e.PlayerId));
        db.ArenaTeamMembers.RemoveRange(toRemove);

        foreach (var memberDto in dto.Members)
        {
            var member = existing.FirstOrDefault(e => e.PlayerId == memberDto.ArenaPlayerId);
            if (member is null)
            {
                db.ArenaTeamMembers.Add(new ArenaTeamMember
                {
                    TeamId = dto.Id,
                    PlayerId = memberDto.ArenaPlayerId,
                    RewardMoneyInfo = memberDto.RewardMoneyInfo
                });
            }
            else
            {
                member.RewardMoneyInfo = memberDto.RewardMoneyInfo;
            }
        }
    }

    private async Task UpsertPlayerAsync(ArenaPlayerDto dto)
    {
        var player = await db.ArenaPlayers.FindAsync(dto.Id);
        if (player is null)
        {
            player = new ArenaPlayer { Id = dto.Id };
            db.ArenaPlayers.Add(player);
        }

        player.TeamId = dto.TeamId;
        player.Cls = dto.Cls;
        player.RewardMoney = dto.RewardMoney;
        player.WeekResetTimestamp = dto.WeekResetTimestamp;
        player.LastBattleTimestamp = dto.LastBattleTimestamp;
        player.LastVisiteTimestamp = dto.LastVisiteTimestamp;
        player.UpdatedAt = DateTime.UtcNow;
    }

    private async Task UpsertBattleStatsAsync(long entityId, EntityType entityType, ArenaBattleInfoDto dto)
    {
        var stats = await db.ArenaBattleStats.FindAsync(entityId, entityType, dto.MatchPattern);
        if (stats is null)
        {
            stats = new ArenaBattleStats
            {
                EntityId = entityId,
                EntityType = entityType,
                MatchPattern = dto.MatchPattern
            };
            db.ArenaBattleStats.Add(stats);
        }

        stats.Score = dto.Score;
        stats.WinCount = dto.WinCount;
        stats.BattleCount = dto.BattleCount;
        stats.WeekBattleCount = dto.WeekBattleCount;
        stats.WeekWinCount = dto.WeekWinCount;
        stats.WeekMaxScore = dto.WeekMaxScore;
        stats.Rank = dto.Rank;
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

    private async Task SaveMatchAsync(long matchId, long teamId, int matchPattern,
        bool isWin, int scoreBefore, int scoreAfter,
        List<(long PlayerId, int Cls, int? ScoreBefore, int? ScoreAfter)> participants)
    {
        var existing = await db.ArenaMatches.FindAsync(matchId);

        if (existing is null)
        {
            db.ArenaMatches.Add(new ArenaMatch
            {
                Id = matchId,
                MatchPattern = matchPattern,
                TeamAId = teamId,
                TeamAScoreBefore = scoreBefore,
                TeamAScoreAfter = scoreAfter,
                WinnerTeamId = isWin ? teamId : null,
                LoserTeamId = isWin ? null : teamId,
            });
        }
        else
        {
            existing.TeamBId = teamId;
            existing.TeamBScoreBefore = scoreBefore;
            existing.TeamBScoreAfter = scoreAfter;

            if (isWin)
                existing.WinnerTeamId = teamId;
            else
                existing.LoserTeamId = teamId;
        }

        foreach (var (playerId, cls, pScoreBefore, pScoreAfter) in participants)
        {
            db.ArenaMatchParticipants.Add(new ArenaMatchParticipant
            {
                MatchId = matchId,
                TeamId = teamId,
                PlayerId = playerId,
                PlayerCls = cls,
                ScoreBefore = pScoreBefore,
                ScoreAfter = pScoreAfter,
                IsWinner = isWin,
            });
        }

        logger.LogInformation("Match {MatchId}: Team {TeamId} {Result} (score {Before} -> {After}, {Count} participants)",
            matchId, teamId, isWin ? "WIN" : "LOSS", scoreBefore, scoreAfter, participants.Count);
    }

    private Task RecordScoreHistoryAsync(long entityId, EntityType entityType, ArenaBattleInfoDto dto)
    {
        db.ArenaScoreHistory.Add(new ArenaScoreHistory
        {
            EntityId = entityId,
            EntityType = entityType,
            MatchPattern = dto.MatchPattern,
            Score = dto.Score,
            WinCount = dto.WinCount,
            BattleCount = dto.BattleCount,
        });
        return Task.CompletedTask;
    }
}
