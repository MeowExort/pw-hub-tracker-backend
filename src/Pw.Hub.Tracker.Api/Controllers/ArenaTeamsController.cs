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
    public async Task<IActionResult> GetById(long teamId, [FromQuery] string? include = null)
    {
        var includeList = (include ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().ToLowerInvariant()).ToList();

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
                    m.PlayerServer,
                    PlayerName = m.Player.Player.Name,
                    PlayerCls = m.Player.Player.Cls,
                    m.RewardMoneyInfo,
                    BattleStats = db.ArenaBattleStats
                        .Where(s => s.EntityId == m.PlayerId && s.Server == m.PlayerServer && s.EntityType == EntityType.Player)
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
                    .Where(s => s.EntityId == t.Id && s.EntityType == EntityType.Team
                        && s.Server == (
                            t.ZoneId == 2 ? "centaur" :
                            t.ZoneId == 3 ? "alkor" :
                            t.ZoneId == 5 ? "mizar" :
                            t.ZoneId == 29 ? "capella" : ""))
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

        object? scoreHistory = null;
        if (includeList.Contains("scorehistory"))
        {
            scoreHistory = await db.ArenaScoreHistory
                .Where(h => h.EntityId == teamId && h.EntityType == EntityType.Team)
                .OrderByDescending(h => h.RecordedAt)
                .Take(200)
                .Select(h => new
                {
                    h.MatchPattern,
                    h.Score,
                    h.WinCount,
                    h.BattleCount,
                    h.RecordedAt
                })
                .ToListAsync();
        }

        return Ok(new
        {
            team.Id,
            team.CaptainId,
            team.ZoneId,
            team.Name,
            team.WeekResetTimestamp,
            team.LastVisiteTimestamp,
            team.UpdatedAt,
            team.Members,
            team.BattleStats,
            ScoreHistory = scoreHistory
        });
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
                    Server = m.Player.PlayerServer,
                    m.Player.Player.Name,
                    m.Player.Player.Cls,
                    m.Player.LastBattleTimestamp,
                    m.Player.LastVisiteTimestamp,
                    BattleStats = db.ArenaBattleStats
                        .Where(s => s.EntityId == m.PlayerId && s.Server == m.PlayerServer && s.EntityType == EntityType.Player)
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
                TeamAName = m.TeamA.Name,
                m.TeamBId,
                TeamBName = m.TeamB.Name,
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
}
