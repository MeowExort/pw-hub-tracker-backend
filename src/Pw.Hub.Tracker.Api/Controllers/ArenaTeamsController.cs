using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Tracker.Domain.Entities;
using Pw.Hub.Tracker.Infrastructure.Data;

namespace Pw.Hub.Tracker.Api.Controllers;

[ApiController]
[Route("api/arena/teams")]
public class ArenaTeamsController(TrackerDbContext db) : ControllerBase
{
    private static readonly Dictionary<char, char> LayoutMap = BuildLayoutMap();

    private static Dictionary<char, char> BuildLayoutMap()
    {
        const string en = "qwertyuiop[]asdfghjkl;'zxcvbnm,.`QWERTYUIOP{}ASDFGHJKL:\"ZXCVBNM<>~";
        const string ru = "йцукенгшщзхъфывапролджэячсмитьбюёЙЦУКЕНГШЩЗХЪФЫВАПРОЛДЖЭЯЧСМИТЬБЮЁ";
        var map = new Dictionary<char, char>();
        for (var i = 0; i < en.Length && i < ru.Length; i++)
        {
            map[en[i]] = ru[i];
            map[ru[i]] = en[i];
        }
        return map;
    }

    private static string NormalizeLayout(string input)
    {
        var chars = input.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (LayoutMap.TryGetValue(chars[i], out var mapped))
                chars[i] = mapped;
        }
        return new string(chars);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string name,
        [FromQuery] int? zoneId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Parameter 'name' is required.");

        pageSize = Math.Clamp(pageSize, 1, 100);
        var trimmed = name.Trim();
        var alternate = NormalizeLayout(trimmed);

        var query = db.ArenaTeams
            .Where(t => t.Name != null)
            .Where(t =>
                EF.Functions.ILike(t.Name!, $"%{trimmed}%") ||
                EF.Functions.ILike(t.Name!, $"%{alternate}%") ||
                EF.Functions.TrigramsSimilarity(t.Name!, trimmed) > 0.3 ||
                EF.Functions.TrigramsSimilarity(t.Name!, alternate) > 0.3);

        if (zoneId.HasValue)
            query = query.Where(t => t.ZoneId == zoneId.Value);

        var total = await query.CountAsync();

        var teams = await query
            .OrderByDescending(t =>
                EF.Functions.TrigramsSimilarity(t.Name!, trimmed) >
                EF.Functions.TrigramsSimilarity(t.Name!, alternate)
                    ? EF.Functions.TrigramsSimilarity(t.Name!, trimmed)
                    : EF.Functions.TrigramsSimilarity(t.Name!, alternate))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.CaptainId,
                t.ZoneId,
                t.Name,
                t.WeekResetTimestamp,
                t.LastVisiteTimestamp,
                t.UpdatedAt,
                MemberCount = t.Members.Count,
                RatingChaos = t.BattleStats
                    .Where(s => s.MatchPattern == 1)
                    .Select(s => t.Members.Count > 0 ? (double)s.Score / t.Members.Count : 0)
                    .FirstOrDefault(),
                RatingOrder = t.BattleStats
                    .Where(s => s.MatchPattern == 0)
                    .Select(s => t.Members.Count > 0 ? (double)s.Score / t.Members.Count : 0)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items = teams });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? zoneId,
        [FromQuery] string? sortBy,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.ArenaTeams.AsQueryable();

        if (zoneId.HasValue)
            query = query.Where(t => t.ZoneId == zoneId.Value);

        var total = await query.CountAsync();

        int? sortMatchPattern = sortBy?.ToLowerInvariant() switch
        {
            "ratingorder" => 0,
            "ratingchaos" => 1,
            _ => null
        };

        IQueryable<object> projected;

        if (sortMatchPattern.HasValue)
        {
            var mp = sortMatchPattern.Value;
            projected = query
                .Select(t => new
                {
                    t.Id,
                    t.CaptainId,
                    t.ZoneId,
                    t.Name,
                    t.WeekResetTimestamp,
                    t.LastVisiteTimestamp,
                    t.UpdatedAt,
                    MemberCount = t.Members.Count,
                    RatingChaos = t.BattleStats
                        .Where(s => s.MatchPattern == 1)
                        .Select(s => t.Members.Count > 0 ? (double)s.Score / t.Members.Count : 0)
                        .FirstOrDefault(),
                    RatingOrder = t.BattleStats
                        .Where(s => s.MatchPattern == 0)
                        .Select(s => t.Members.Count > 0 ? (double)s.Score / t.Members.Count : 0)
                        .FirstOrDefault(),
                    RealRating = t.BattleStats
                        .Where(s => s.MatchPattern == mp)
                        .Select(s => t.Members.Count > 0 ? (double)s.Score / t.Members.Count : 0)
                        .FirstOrDefault()
                })
                .OrderByDescending(t => t.RealRating)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);
        }
        else
        {
            projected = query
                .OrderByDescending(t => t.UpdatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    t.Id,
                    t.CaptainId,
                    t.ZoneId,
                    t.Name,
                    t.WeekResetTimestamp,
                    t.LastVisiteTimestamp,
                    t.UpdatedAt,
                    MemberCount = t.Members.Count,
                    RatingChaos = t.BattleStats
                        .Where(s => s.MatchPattern == 1)
                        .Select(s => t.Members.Count > 0 ? (double)s.Score / t.Members.Count : 0)
                        .FirstOrDefault(),
                    RatingOrder = t.BattleStats
                        .Where(s => s.MatchPattern == 0)
                        .Select(s => t.Members.Count > 0 ? (double)s.Score / t.Members.Count : 0)
                        .FirstOrDefault()
                });
        }

        var teams = await projected.ToListAsync();

        return Ok(new { total, page, pageSize, items = teams });
    }

    [HttpGet("{teamId:long}")]
    public async Task<IActionResult> GetById(long teamId)
    {
        var team = await db.ArenaTeams
            .Where(t => t.Id == teamId)
            .Select(t => new
            {
                t.Id,
                t.CaptainId,
                t.ZoneId,
                t.Name,
                t.WeekResetTimestamp,
                t.LastVisiteTimestamp,
                t.UpdatedAt,
                Members = t.Members.Select(m => new
                {
                    m.PlayerId,
                    PlayerName = m.Player.Name,
                    PlayerCls = m.Player.Cls,
                    m.RewardMoneyInfo,
                    BattleStats = db.ArenaBattleStats
                        .Where(s => s.EntityId == m.PlayerId && s.EntityType == EntityType.Player)
                        .Select(s => new
                        {
                            s.MatchPattern,
                            s.Score,
                            s.WinCount,
                            s.BattleCount,
                            s.WeekBattleCount,
                            s.WeekWinCount,
                            s.WeekMaxScore,
                            s.Rank
                        }).ToList()
                }).ToList(),
                BattleStats = db.ArenaBattleStats
                    .Where(s => s.EntityId == t.Id && s.EntityType == EntityType.Team)
                    .Select(s => new
                    {
                        s.MatchPattern,
                        s.Score,
                        s.WinCount,
                        s.BattleCount,
                        s.WeekBattleCount,
                        s.WeekWinCount,
                        s.WeekMaxScore,
                        s.Rank
                    }).ToList()
            })
            .FirstOrDefaultAsync();

        if (team is null)
            return NotFound();

        return Ok(team);
    }

    [HttpGet("{teamId:long}/members")]
    public async Task<IActionResult> GetMembers(long teamId)
    {
        var members = await db.ArenaTeamMembers
            .Where(m => m.TeamId == teamId)
            .Select(m => new
            {
                m.PlayerId,
                m.RewardMoneyInfo,
                Player = new
                {
                    m.Player.Id,
                    m.Player.Name,
                    m.Player.Cls,
                    m.Player.LastBattleTimestamp,
                    m.Player.LastVisiteTimestamp,
                    BattleStats = db.ArenaBattleStats
                        .Where(s => s.EntityId == m.PlayerId && s.EntityType == EntityType.Player)
                        .Select(s => new
                        {
                            s.MatchPattern,
                            s.Score,
                            s.WinCount,
                            s.BattleCount,
                            s.WeekBattleCount,
                            s.WeekWinCount,
                            s.WeekMaxScore,
                            s.Rank
                        }).ToList()
                }
            })
            .ToListAsync();

        return Ok(members);
    }

    [HttpGet("{teamId:long}/matches")]
    public async Task<IActionResult> GetMatches(
        long teamId,
        [FromQuery] int? matchPattern,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.ArenaMatches
            .Where(m => m.TeamAId == teamId || m.TeamBId == teamId);

        if (matchPattern.HasValue)
            query = query.Where(m => m.MatchPattern == matchPattern.Value);

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

    [HttpGet("{teamId:long}/score-history")]
    public async Task<IActionResult> GetScoreHistory(
        long teamId,
        [FromQuery] int? matchPattern,
        [FromQuery] int limit = 100)
    {
        limit = Math.Clamp(limit, 1, 1000);

        var query = db.ArenaScoreHistory
            .Where(h => h.EntityId == teamId && h.EntityType == EntityType.Team);

        if (matchPattern.HasValue)
            query = query.Where(h => h.MatchPattern == matchPattern.Value);

        var history = await query
            .OrderByDescending(h => h.RecordedAt)
            .Take(limit)
            .Select(h => new
            {
                h.MatchPattern,
                h.Score,
                h.WinCount,
                h.BattleCount,
                h.MemberCount,
                h.RecordedAt
            })
            .ToListAsync();

        return Ok(history);
    }

    [HttpPost("matches/fix-score-changes")]
    public async Task<IActionResult> FixMatchScoreChanges()
    {
        // Загружаем все матчи, у которых есть обе команды
        var matches = await db.ArenaMatches
            .AsTracking()
            .Where(m => m.TeamAId != null && m.TeamBId != null)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        // Загружаем всю историю рейтинга команд, сгруппированную по (EntityId, MatchPattern)
        var scoreHistory = await db.ArenaScoreHistory
            .Where(h => h.EntityType == EntityType.Team)
            .OrderBy(h => h.RecordedAt)
            .ToListAsync();

        // Группируем историю по (EntityId, MatchPattern) для быстрого поиска
        var historyLookup = scoreHistory
            .GroupBy(h => (h.EntityId, h.MatchPattern))
            .ToDictionary(g => g.Key, g => g.ToList());

        var fixedCount = 0;

        foreach (var match in matches)
        {
            var changed = false;

            // Исправляем ScoreBefore/After для TeamA
            if (match.TeamAId.HasValue &&
                historyLookup.TryGetValue((match.TeamAId.Value, match.MatchPattern), out var historyA))
            {
                var (before, after) = FindScoreAroundMatch(historyA, match.CreatedAt);
                if (before.HasValue && match.TeamAScoreBefore != before.Value)
                {
                    match.TeamAScoreBefore = before.Value;
                    changed = true;
                }
                if (after.HasValue && match.TeamAScoreAfter != after.Value)
                {
                    match.TeamAScoreAfter = after.Value;
                    changed = true;
                }
            }

            // Исправляем ScoreBefore/After для TeamB
            if (match.TeamBId.HasValue &&
                historyLookup.TryGetValue((match.TeamBId.Value, match.MatchPattern), out var historyB))
            {
                var (before, after) = FindScoreAroundMatch(historyB, match.CreatedAt);
                if (before.HasValue && match.TeamBScoreBefore != before.Value)
                {
                    match.TeamBScoreBefore = before.Value;
                    changed = true;
                }
                if (after.HasValue && match.TeamBScoreAfter != after.Value)
                {
                    match.TeamBScoreAfter = after.Value;
                    changed = true;
                }
            }

            if (changed)
                fixedCount++;
        }

        if (fixedCount > 0)
            await db.SaveChangesAsync();

        return Ok(new { totalMatches = matches.Count, fixedCount });
    }

    /// <summary>
    /// Находит NormalizedScore (Score/MemberCount) до и после матча по времени.
    /// Берёт ближайшую запись до CreatedAt как "before" и ближайшую после как "after".
    /// Если MemberCount отсутствует, использует сырой Score.
    /// </summary>
    private static (int? Before, int? After) FindScoreAroundMatch(
        List<ArenaScoreHistory> history, DateTime matchTime)
    {
        // history уже отсортирована по RecordedAt ASC
        ArenaScoreHistory? beforeRecord = null;
        ArenaScoreHistory? afterRecord = null;

        foreach (var h in history)
        {
            if (h.RecordedAt <= matchTime)
                beforeRecord = h;
            else
            {
                afterRecord = h;
                break;
            }
        }

        // Если afterRecord не найден, но beforeRecord есть — 
        // возможно матч произошёл после последней записи истории.
        // В этом случае берём последние две записи как before/after.
        if (afterRecord is null && beforeRecord is not null)
        {
            var idx = history.IndexOf(beforeRecord);
            if (idx > 0)
            {
                afterRecord = beforeRecord;
                beforeRecord = history[idx - 1];
            }
        }

        static int? GetNormalizedScore(ArenaScoreHistory? record)
        {
            if (record is null) return null;
            if (record.MemberCount is > 0)
                return (int)Math.Round((double)record.Score / record.MemberCount.Value);
            return record.Score;
        }

        return (GetNormalizedScore(beforeRecord), GetNormalizedScore(afterRecord));
    }

    [HttpPost("score-history/fix-member-counts")]
    public async Task<IActionResult> FixMemberCounts()
    {
        // Текущее количество членов команды — используем как fallback для последней записи
        var teamMemberCounts = await db.ArenaTeamMembers
            .GroupBy(m => m.TeamId)
            .Select(g => new { TeamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TeamId, x => x.Count);

        // Загружаем ВСЮ историю команд, упорядоченную от новых к старым (идём от конца к началу)
        var allHistory = await db.ArenaScoreHistory
            .AsTracking()
            .Where(h => h.EntityType == EntityType.Team)
            .OrderBy(h => h.EntityId)
            .ThenBy(h => h.MatchPattern)
            .ThenByDescending(h => h.RecordedAt)
            .ToListAsync();

        var fixedCount = 0;
        var details = new List<object>();

        ArenaScoreHistory? prevTeamHistory = null;
        for (var i = 0; i < allHistory.Count; i++)
        {
            var current = allHistory[i];
            var isSameGroup = prevTeamHistory is not null
                              && prevTeamHistory.EntityId == current.EntityId
                              && prevTeamHistory.MatchPattern == current.MatchPattern;

            if (current.MemberCount == null)
            {
                if (!isSameGroup)
                {
                    // Самая актуальная запись группы — ставим текущее количество игроков
                    if (teamMemberCounts.TryGetValue(current.EntityId, out var count))
                        current.MemberCount = count;
                }
                else
                {
                    // prevTeamHistory — более новая запись (уже обработана)
                    // current — более старая запись
                    // diff = prevTeamHistory.Score - current.Score
                    var scoreDiff = Math.Abs(prevTeamHistory!.Score - current.Score);
                    if (scoreDiff < 1000)
                    {
                        current.MemberCount = prevTeamHistory.MemberCount;
                    }
                    else
                    {
                        var memberDiff = scoreDiff / 1000;
                        current.MemberCount = prevTeamHistory.MemberCount - memberDiff;
                    }
                }

                if (current.MemberCount.HasValue)
                    fixedCount++;
            }

            prevTeamHistory = current;
        }

        if (fixedCount > 0)
            await db.SaveChangesAsync();

        return Ok(new { totalRecords = allHistory.Count, fixedCount, details });
    }
}
