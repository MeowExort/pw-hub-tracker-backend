using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Tracker.Domain.Entities;
using Pw.Hub.Tracker.Infrastructure.Data;

namespace Pw.Hub.Tracker.Api.Controllers;

[ApiController]
[Route("api/arena/matches")]
public class ArenaMatchesController(TrackerDbContext db, ILogger<ArenaMatchesController> logger) : ControllerBase
{
    [HttpGet("{matchId:long}")]
    public async Task<IActionResult> GetById(long matchId)
    {
        var match = await db.ArenaMatches
            .Where(m => m.Id == matchId)
            .Select(m => new
            {
                m.Id,
                m.MatchPattern,
                m.TeamAId,
                m.TeamBId,
                m.WinnerTeamId,
                m.LoserTeamId,
                m.TeamAScoreBefore,
                m.TeamAScoreAfter,
                m.TeamBScoreBefore,
                m.TeamBScoreAfter,
                m.CreatedAt,
                Participants = m.Participants.Select(p => new
                {
                    p.PlayerId,
                    PlayerName = p.Player.Player.Name,
                    p.TeamId,
                    p.PlayerCls,
                    p.ScoreBefore,
                    p.ScoreAfter,
                    p.IsWinner
                })
            })
            .FirstOrDefaultAsync();

        if (match is null)
            return NotFound();

        return Ok(match);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? matchPattern,
        [FromQuery] long? teamId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.ArenaMatches.AsQueryable();

        if (matchPattern.HasValue)
            query = query.Where(m => m.MatchPattern == matchPattern.Value);

        if (teamId.HasValue)
            query = query.Where(m => m.TeamAId == teamId.Value || m.TeamBId == teamId.Value);

        var total = await query.CountAsync();

        var matches = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.MatchPattern,
                m.TeamAId,
                m.TeamBId,
                m.WinnerTeamId,
                m.LoserTeamId,
                m.TeamAScoreBefore,
                m.TeamAScoreAfter,
                m.TeamBScoreBefore,
                m.TeamBScoreAfter,
                m.CreatedAt
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items = matches });
    }

    [HttpPost("rebuild")]
    public async Task<IActionResult> Rebuild()
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        try
        {
            // 1. Удаляем все существующие матчи и участников
            await db.Database.ExecuteSqlRawAsync("""DELETE FROM arena_match_participants""");
            await db.Database.ExecuteSqlRawAsync("""DELETE FROM arena_matches""");

            // 2. Загружаем игроков и определяем lastBattleTimestamp для каждой команды
            var players = await db.ArenaPlayers
                .Include(p => p.Player)
                .Where(p => p.LastBattleTimestamp > 0)
                .ToListAsync();

            // teamId -> max(LastBattleTimestamp)
            var teamTimestamps = players
                .GroupBy(p => p.TeamId)
                .ToDictionary(g => g.Key, g => g.Max(p => p.LastBattleTimestamp));

            // 3. Группируем команды по LastBattleTimestamp — пары с одинаковым timestamp играли друг против друга
            var matchPairs = teamTimestamps
                .GroupBy(kv => kv.Value)
                .Where(g => g.Count() == 2)
                .Select(g => g.Select(kv => kv.Key).OrderBy(id => id).ToArray())
                .ToList();

            // 4. Загружаем battle_stats для команд
            var battleStats = await db.ArenaBattleStats
                .Where(s => s.EntityType == EntityType.Team)
                .ToListAsync();

            var statsLookup = battleStats
                .GroupBy(s => s.EntityId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 5. Группируем игроков по команде для быстрого доступа
            var playersByTeam = players
                .GroupBy(p => p.TeamId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 6. Создаём матчи и участников для каждой пары и каждого matchPattern
            var matches = new List<ArenaMatch>();
            var allParticipants = new List<ArenaMatchParticipant>();
            var skippedPairs = 0;

            foreach (var pair in matchPairs)
            {
                var teamAId = pair[0];
                var teamBId = pair[1];
                var battleTimestamp = teamTimestamps[teamAId];

                // Находим общие matchPattern у обеих команд
                var statsA = statsLookup.GetValueOrDefault(teamAId, []);
                var statsB = statsLookup.GetValueOrDefault(teamBId, []);

                var patternsA = statsA.ToDictionary(s => s.MatchPattern);
                var patternsB = statsB.ToDictionary(s => s.MatchPattern);

                var commonPatterns = patternsA.Keys.Intersect(patternsB.Keys);

                foreach (var matchPattern in commonPatterns)
                {
                    var statA = patternsA[matchPattern];
                    var statB = patternsB[matchPattern];

                    var matchId = GenerateMatchId(battleTimestamp, matchPattern);

                    var match = new ArenaMatch
                    {
                        Id = matchId,
                        MatchPattern = matchPattern,
                        TeamAId = teamAId,
                        TeamBId = teamBId,
                        TeamAScoreBefore = statA.Score,
                        TeamAScoreAfter = statA.Score,
                        TeamBScoreBefore = statB.Score,
                        TeamBScoreAfter = statB.Score,
                        CreatedAt = DateTime.UtcNow
                    };

                    matches.Add(match);

                    // Добавляем участников — игроков обеих команд с LastBattleTimestamp матча
                    foreach (var (teamId, isTeamA) in new[] { (teamAId, true), (teamBId, false) })
                    {
                        var teamPlayersList = playersByTeam.GetValueOrDefault(teamId, []);
                        foreach (var p in teamPlayersList.Where(p => p.LastBattleTimestamp == battleTimestamp))
                        {
                            allParticipants.Add(new ArenaMatchParticipant
                            {
                                MatchId = matchId,
                                TeamId = teamId,
                                PlayerId = p.Id,
                                PlayerCls = p.Player.Cls,
                                ScoreBefore = null,
                                ScoreAfter = isTeamA ? statA.Score : statB.Score,
                                IsWinner = false
                            });
                        }
                    }
                }
            }

            // Команды без пары (уникальный timestamp)
            var pairedTeamIds = matchPairs.SelectMany(p => p).ToHashSet();
            var unpairedTeams = teamTimestamps.Keys.Except(pairedTeamIds).ToList();

            // 7. Сохраняем матчи и участников
            db.ArenaMatches.AddRange(matches);
            db.ArenaMatchParticipants.AddRange(allParticipants);
            await db.SaveChangesAsync();

            await transaction.CommitAsync();

            logger.LogInformation("Rebuilt {Count} matches from player timestamps ({Pairs} pairs, {Unpaired} unpaired teams)",
                matches.Count, matchPairs.Count, unpairedTeams.Count);

            return Ok(new
            {
                matchesCreated = matches.Count,
                participantsCreated = allParticipants.Count,
                teamPairsFound = matchPairs.Count,
                unpairedTeams = unpairedTeams.Count,
                totalTeamsWithBattles = teamTimestamps.Count
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Failed to rebuild matches");
            throw;
        }
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
}
