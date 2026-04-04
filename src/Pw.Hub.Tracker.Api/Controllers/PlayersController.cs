using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Tracker.Domain.Entities;
using Pw.Hub.Tracker.Infrastructure.Data;
using Pw.Hub.Tracker.Infrastructure.Helpers;

namespace Pw.Hub.Tracker.Api.Controllers;

public class GetPlayersParams
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? Server { get; set; }
    public int? Cls { get; set; }
    public string SortBy { get; set; } = "hp";
    public string SortOrder { get; set; } = "desc";
    
    public long? HpMin { get; set; }
    public long? HpMax { get; set; }
    public long? DefenseMin { get; set; }
    public long? DefenseMax { get; set; }
    public long? ResistanceMin { get; set; }
    public long? ResistanceMax { get; set; }
    public long? DamageLowMin { get; set; }
    public long? DamageHighMax { get; set; }
    public long? DamageMagicLowMin { get; set; }
    public long? DamageMagicHighMax { get; set; }
    public int? AttackDegreeMin { get; set; }
    public int? AttackDegreeMax { get; set; }
    public int? DefendDegreeMin { get; set; }
    public int? DefendDegreeMax { get; set; }
    public long? VigourMin { get; set; }
    public long? VigourMax { get; set; }
    public int? AntiDefenseDegreeMin { get; set; }
    public int? AntiDefenseDegreeMax { get; set; }
    public int? AntiResistanceDegreeMin { get; set; }
    public int? AntiResistanceDegreeMax { get; set; }
    public int? PeakGradeMin { get; set; }
    public int? PeakGradeMax { get; set; }
}

public class PlayerListItem
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public int Cls { get; set; }
    public string? Server { get; set; }
    public long? TeamId { get; set; }
    public string? TeamName { get; set; }
    public PlayerPropertiesDto? Properties { get; set; }
}

public class PlayerPropertiesDto
{
    public long Hp { get; set; }
    public long Mp { get; set; }
    public long DamageLow { get; set; }
    public long DamageHigh { get; set; }
    public long DamageMagicLow { get; set; }
    public long DamageMagicHigh { get; set; }
    public long Defense { get; set; }
    public long[] Resistance { get; set; } = [];
    public int AttackDegree { get; set; }
    public int DefendDegree { get; set; }
    public long Vigour { get; set; }
    public int AntiDefenseDegree { get; set; }
    public int AntiResistanceDegree { get; set; }
    public int PeakGrade { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class PlayersController(TrackerDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPlayers([FromQuery] GetPlayersParams p)
    {
        var pageSize = Math.Clamp(p.PageSize, 1, 100);
        var page = Math.Max(p.Page, 1);

        var query = db.Players.AsQueryable();

        // Group PlayerPropertyHistory to get max values for each property per player
        var propsMaxQuery = db.PlayerPropertyHistory
            .GroupBy(ph => new { ph.PlayerId, ph.Server })
            .Select(g => new
            {
                g.Key.PlayerId,
                g.Key.Server,
                Hp = g.Max(ph => ph.Hp),
                Mp = g.Max(ph => ph.Mp),
                DamageLow = g.Max(ph => ph.DamageLow),
                DamageHigh = g.Max(ph => ph.DamageHigh),
                DamageMagicLow = g.Max(ph => ph.DamageMagicLow),
                DamageMagicHigh = g.Max(ph => ph.DamageMagicHigh),
                Defense = g.Max(ph => ph.Defense),
                // For resistance, we typically want the latest one or handle it specifically.
                // Based on previous requirements, we'll take the resistance from the most recent entry.
                Resistance = g.OrderByDescending(ph => ph.RecordedAt).Select(ph => ph.Resistance).FirstOrDefault(),
                AttackDegree = g.Max(ph => ph.AttackDegree),
                DefendDegree = g.Max(ph => ph.DefendDegree),
                Vigour = g.Max(ph => ph.Vigour),
                AntiDefenseDegree = g.Max(ph => ph.AntiDefenseDegree),
                AntiResistanceDegree = g.Max(ph => ph.AntiResistanceDegree),
                PeakGrade = g.Max(ph => ph.PeakGrade),
                UpdatedAt = g.Max(ph => ph.RecordedAt)
            });

        // Join with aggregated properties and optionally with arena players/teams
        var extendedQuery = db.Players
            .GroupJoin(propsMaxQuery, 
                p => new { p.Id, p.Server }, 
                pp => new { Id = pp.PlayerId, pp.Server }, 
                (player, propsJoin) => new { player, propsJoin })
            .SelectMany(x => x.propsJoin.DefaultIfEmpty(), (x, props) => new { x.player, props })
            .GroupJoin(db.ArenaPlayers,
                x => new { x.player.Id, PlayerServer = x.player.Server },
                ap => new { ap.Id, ap.PlayerServer },
                (x, apJoin) => new { x.player, x.props, apJoin })
            .SelectMany(x => x.apJoin.DefaultIfEmpty(), (x, ap) => new { x.player, x.props, ap })
            .GroupJoin(db.ArenaTeams,
                x => x.ap != null ? (long?)x.ap.TeamId : null,
                t => (long?)t.Id,
                (x, teamJoin) => new { x.player, x.props, x.ap, teamJoin })
            .SelectMany(x => x.teamJoin.DefaultIfEmpty(), (x, team) => new { x.player, x.props, team });

        // Filtering
        if (!string.IsNullOrWhiteSpace(p.Server))
            extendedQuery = extendedQuery.Where(x => x.player.Server == p.Server);

        if (p.Cls.HasValue)
            extendedQuery = extendedQuery.Where(x => x.player.Cls == p.Cls.Value);

        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var normalized = HomoglyphHelper.Normalize(p.Search.Trim());
            extendedQuery = extendedQuery.Where(x => 
                EF.Functions.ILike(x.player.Name, $"%{p.Search}%") || 
                EF.Functions.ILike(x.player.Name, $"%{normalized}%"));
        }

        // Property filters
        if (p.HpMin.HasValue) extendedQuery = extendedQuery.Where(x => x.props.Hp >= p.HpMin.Value);
        if (p.HpMax.HasValue) extendedQuery = extendedQuery.Where(x => x.props.Hp <= p.HpMax.Value);
        
        if (p.DefenseMin.HasValue) extendedQuery = extendedQuery.Where(x => x.props.Defense >= p.DefenseMin.Value);
        if (p.DefenseMax.HasValue) extendedQuery = extendedQuery.Where(x => x.props.Defense <= p.DefenseMax.Value);

        if (p.ResistanceMin.HasValue) extendedQuery = extendedQuery.Where(x => x.props.Resistance.Any() && x.props.Resistance.Max() >= p.ResistanceMin.Value);
        if (p.ResistanceMax.HasValue) extendedQuery = extendedQuery.Where(x => x.props.Resistance.Any() && x.props.Resistance.Max() <= p.ResistanceMax.Value);

        if (p.DamageLowMin.HasValue) extendedQuery = extendedQuery.Where(x => x.props.DamageLow >= p.DamageLowMin.Value);
        if (p.DamageHighMax.HasValue) extendedQuery = extendedQuery.Where(x => x.props.DamageHigh <= p.DamageHighMax.Value);

        if (p.DamageMagicLowMin.HasValue) extendedQuery = extendedQuery.Where(x => x.props.DamageMagicLow >= p.DamageMagicLowMin.Value);
        if (p.DamageMagicHighMax.HasValue) extendedQuery = extendedQuery.Where(x => x.props.DamageMagicHigh <= p.DamageMagicHighMax.Value);

        if (p.AttackDegreeMin.HasValue) extendedQuery = extendedQuery.Where(x => x.props.AttackDegree >= p.AttackDegreeMin.Value);
        if (p.AttackDegreeMax.HasValue) extendedQuery = extendedQuery.Where(x => x.props.AttackDegree <= p.AttackDegreeMax.Value);

        if (p.DefendDegreeMin.HasValue) extendedQuery = extendedQuery.Where(x => x.props.DefendDegree >= p.DefendDegreeMin.Value);
        if (p.DefendDegreeMax.HasValue) extendedQuery = extendedQuery.Where(x => x.props.DefendDegree <= p.DefendDegreeMax.Value);

        if (p.VigourMin.HasValue) extendedQuery = extendedQuery.Where(x => x.props.Vigour >= p.VigourMin.Value);
        if (p.VigourMax.HasValue) extendedQuery = extendedQuery.Where(x => x.props.Vigour <= p.VigourMax.Value);

        if (p.AntiDefenseDegreeMin.HasValue) extendedQuery = extendedQuery.Where(x => x.props.AntiDefenseDegree >= p.AntiDefenseDegreeMin.Value);
        if (p.AntiDefenseDegreeMax.HasValue) extendedQuery = extendedQuery.Where(x => x.props.AntiDefenseDegree <= p.AntiDefenseDegreeMax.Value);

        if (p.AntiResistanceDegreeMin.HasValue) extendedQuery = extendedQuery.Where(x => x.props.AntiResistanceDegree >= p.AntiResistanceDegreeMin.Value);
        if (p.AntiResistanceDegreeMax.HasValue) extendedQuery = extendedQuery.Where(x => x.props.AntiResistanceDegree <= p.AntiResistanceDegreeMax.Value);

        if (p.PeakGradeMin.HasValue) extendedQuery = extendedQuery.Where(x => x.props.PeakGrade >= p.PeakGradeMin.Value);
        if (p.PeakGradeMax.HasValue) extendedQuery = extendedQuery.Where(x => x.props.PeakGrade <= p.PeakGradeMax.Value);

        // Sorting
        var isAsc = p.SortOrder?.ToLower() == "asc";
        
        extendedQuery = p.SortBy.ToLower() switch
        {
            "hp" => isAsc ? extendedQuery.OrderBy(x => x.props.Hp) : extendedQuery.OrderByDescending(x => x.props.Hp),
            "defense" => isAsc ? extendedQuery.OrderBy(x => x.props.Defense) : extendedQuery.OrderByDescending(x => x.props.Defense),
            "resistance" => isAsc ? extendedQuery.OrderBy(x => x.props.Resistance.Max()) : extendedQuery.OrderByDescending(x => x.props.Resistance.Max()),
            "damage" => isAsc 
                ? extendedQuery.OrderBy(x => (x.props.DamageLow + x.props.DamageHigh) / 2.0) 
                : extendedQuery.OrderByDescending(x => (x.props.DamageLow + x.props.DamageHigh) / 2.0),
            "damagemagic" => isAsc 
                ? extendedQuery.OrderBy(x => (x.props.DamageMagicLow + x.props.DamageMagicHigh) / 2.0) 
                : extendedQuery.OrderByDescending(x => (x.props.DamageMagicLow + x.props.DamageMagicHigh) / 2.0),
            "attackdegree" => isAsc ? extendedQuery.OrderBy(x => x.props.AttackDegree) : extendedQuery.OrderByDescending(x => x.props.AttackDegree),
            "defenddegree" => isAsc ? extendedQuery.OrderBy(x => x.props.DefendDegree) : extendedQuery.OrderByDescending(x => x.props.DefendDegree),
            "vigour" => isAsc ? extendedQuery.OrderBy(x => x.props.Vigour) : extendedQuery.OrderByDescending(x => x.props.Vigour),
            "antidefensedegree" => isAsc ? extendedQuery.OrderBy(x => x.props.AntiDefenseDegree) : extendedQuery.OrderByDescending(x => x.props.AntiDefenseDegree),
            "antiresistancedegree" => isAsc ? extendedQuery.OrderBy(x => x.props.AntiResistanceDegree) : extendedQuery.OrderByDescending(x => x.props.AntiResistanceDegree),
            "peakgrade" => isAsc ? extendedQuery.OrderBy(x => x.props.PeakGrade) : extendedQuery.OrderByDescending(x => x.props.PeakGrade),
            _ => isAsc ? extendedQuery.OrderBy(x => x.props.Hp) : extendedQuery.OrderByDescending(x => x.props.Hp)
        };

        var total = await extendedQuery.CountAsync();
        
        var items = await extendedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PlayerListItem
            {
                Id = x.player.Id,
                Name = x.player.Name,
                Cls = x.player.Cls,
                Server = x.player.Server,
                TeamId = x.team != null ? x.team.Id : null,
                TeamName = x.team != null ? x.team.Name : null,
                Properties = x.props != null ? new PlayerPropertiesDto
                {
                    Hp = x.props.Hp,
                    Mp = x.props.Mp,
                    DamageLow = x.props.DamageLow,
                    DamageHigh = x.props.DamageHigh,
                    DamageMagicLow = x.props.DamageMagicLow,
                    DamageMagicHigh = x.props.DamageMagicHigh,
                    Defense = x.props.Defense,
                    Resistance = x.props.Resistance,
                    AttackDegree = x.props.AttackDegree,
                    DefendDegree = x.props.DefendDegree,
                    Vigour = x.props.Vigour,
                    AntiDefenseDegree = x.props.AntiDefenseDegree,
                    AntiResistanceDegree = x.props.AntiResistanceDegree,
                    PeakGrade = x.props.PeakGrade,
                    UpdatedAt = x.props.UpdatedAt
                } : null
            })
            .ToListAsync();

        return Ok(new
        {
            total,
            page,
            pageSize,
            items
        });
    }
}
