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

            // 2. Загружаем историю score для команд, упорядоченную по времени
            var history = await db.ArenaScoreHistory
                .Where(h => h.EntityType == EntityType.Team)
                .OrderBy(h => h.RecordedAt)
                .ToListAsync();

            // 3. Группируем по (EntityId, MatchPattern) и находим моменты увеличения BattleCount
            var matchEvents = new List<MatchEvent>();

            var grouped = history.GroupBy(h => new { h.EntityId, h.MatchPattern });
            foreach (var group in grouped)
            {
                var records = group.OrderBy(r => r.RecordedAt).ToList();
                for (var i = 1; i < records.Count; i++)
                {
                    if (records[i].BattleCount > records[i - 1].BattleCount)
                    {
                        var isWin = records[i].WinCount > records[i - 1].WinCount;
                        matchEvents.Add(new MatchEvent
                        {
                            TeamId = group.Key.EntityId,
                            MatchPattern = group.Key.MatchPattern,
                            ScoreBefore = records[i - 1].Score,
                            ScoreAfter = records[i].Score,
                            IsWin = isWin,
                            RecordedAt = records[i].RecordedAt
                        });
                    }
                }
            }

            // 4. Группируем события в пары по MatchPattern и близкому RecordedAt (в пределах 60 секунд)
            var usedEvents = new HashSet<int>();
            var matches = new List<ArenaMatch>();

            var byPattern = matchEvents
                .Select((e, idx) => (Event: e, Index: idx))
                .GroupBy(x => x.Event.MatchPattern);

            foreach (var patternGroup in byPattern)
            {
                var events = patternGroup.OrderBy(x => x.Event.RecordedAt).ToList();

                for (var i = 0; i < events.Count; i++)
                {
                    if (usedEvents.Contains(events[i].Index)) continue;

                    var eventA = events[i];
                    (MatchEvent Event, int Index)? eventB = null;

                    // Ищем ближайшее событие другой команды в пределах 60 секунд
                    for (var j = i + 1; j < events.Count; j++)
                    {
                        if (usedEvents.Contains(events[j].Index)) continue;
                        if (events[j].Event.TeamId == eventA.Event.TeamId) continue;

                        var timeDiff = Math.Abs((events[j].Event.RecordedAt - eventA.Event.RecordedAt).TotalSeconds);
                        if (timeDiff <= 60)
                        {
                            eventB = events[j];
                            break;
                        }

                        // Если разница > 60 сек, дальше искать нет смысла (отсортировано)
                        if ((events[j].Event.RecordedAt - eventA.Event.RecordedAt).TotalSeconds > 60)
                            break;
                    }

                    usedEvents.Add(eventA.Index);

                    var matchId = GenerateMatchId(eventA.Event.RecordedAt, eventA.Event.MatchPattern);
                    var match = new ArenaMatch
                    {
                        Id = matchId,
                        MatchPattern = eventA.Event.MatchPattern,
                        TeamAId = eventA.Event.TeamId,
                        TeamAScoreBefore = eventA.Event.ScoreBefore,
                        TeamAScoreAfter = eventA.Event.ScoreAfter,
                        CreatedAt = eventA.Event.RecordedAt
                    };

                    if (eventA.Event.IsWin)
                    {
                        match.WinnerTeamId = eventA.Event.TeamId;
                    }
                    else
                    {
                        match.LoserTeamId = eventA.Event.TeamId;
                    }

                    if (eventB.HasValue)
                    {
                        usedEvents.Add(eventB.Value.Index);
                        match.TeamBId = eventB.Value.Event.TeamId;
                        match.TeamBScoreBefore = eventB.Value.Event.ScoreBefore;
                        match.TeamBScoreAfter = eventB.Value.Event.ScoreAfter;

                        if (eventB.Value.Event.IsWin)
                            match.WinnerTeamId = eventB.Value.Event.TeamId;
                        else
                            match.LoserTeamId = eventB.Value.Event.TeamId;
                    }

                    matches.Add(match);
                }
            }

            // 5. Сохраняем матчи
            db.ArenaMatches.AddRange(matches);
            await db.SaveChangesAsync();

            await transaction.CommitAsync();

            logger.LogInformation("Rebuilt {Count} matches from score history", matches.Count);

            return Ok(new
            {
                matchesCreated = matches.Count,
                matchEventsFound = matchEvents.Count,
                unmatchedEvents = matchEvents.Count - usedEvents.Count
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Failed to rebuild matches");
            throw;
        }
    }

    private static long GenerateMatchId(DateTime recordedAt, int matchPattern)
    {
        var timestamp = new DateTimeOffset(recordedAt, TimeSpan.Zero).ToUnixTimeMilliseconds();
        unchecked
        {
            var hash = 17L;
            hash = hash * 31 + timestamp;
            hash = hash * 31 + matchPattern;
            return hash;
        }
    }

    private record MatchEvent
    {
        public long TeamId { get; init; }
        public int MatchPattern { get; init; }
        public int ScoreBefore { get; init; }
        public int ScoreAfter { get; init; }
        public bool IsWin { get; init; }
        public DateTime RecordedAt { get; init; }
    }
}
