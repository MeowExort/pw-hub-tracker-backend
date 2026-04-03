using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Tracker.Domain.Entities;
using Pw.Hub.Tracker.Infrastructure.Data;

namespace Pw.Hub.Tracker.Api.Controllers;

public class PlayerRequest
{
    public long Id { get; set; }
    public string Server { get; set; }
}

[ApiController]
[Route("api/players/properties")]
public class PlayerPropertiesController(TrackerDbContext db) : ControllerBase
{
    [HttpPost("by-ids")]
    public async Task<IActionResult> GetByIds([FromBody] PlayerRequest[] players)
    {
        if (players is not { Length: > 0 })
            return BadRequest("playerIds must not be empty");

        // Группируем запросы по серверу для оптимизации
        var results = new List<object>();

        foreach (var p in players)
        {
            var maxProps = await db.PlayerPropertyHistory
                .Where(pp => pp.PlayerId == p.Id && pp.Server == p.Server)
                .GroupBy(pp => new { pp.PlayerId, pp.Server })
                .Select(g => new
                {
                    g.Key.PlayerId,
                    PlayerCls = (int?)db.Players.Where(pl => pl.Id == g.Key.PlayerId && pl.Server == g.Key.Server).Select(pl => pl.Cls).FirstOrDefault(),
                    PlayerName = db.Players.Where(pl => pl.Id == g.Key.PlayerId && pl.Server == g.Key.Server).Select(pl => pl.Name).FirstOrDefault(),
                    g.Key.Server,
                    Hp = g.Max(x => x.Hp),
                    Mp = g.Max(x => x.Mp),
                    DamageLow = g.Max(x => x.DamageLow),
                    DamageHigh = g.Max(x => x.DamageHigh),
                    DamageMagicLow = g.Max(x => x.DamageMagicLow),
                    DamageMagicHigh = g.Max(x => x.DamageMagicHigh),
                    Defense = g.Max(x => x.Defense),
                    // Resistance - массив, для него Max не имеет смысла в лоб, берем из последнего или пропускаем? 
                    // В ТЗ сказано "максимальные значения характеристик", обычно имеют ввиду числовые.
                    // Оставляем как в ТЗ - берем из последней записи или максимальные по элементам?
                    // Скорее всего фронту нужно просто актуальное или макс по сумме. 
                    // Но Resistance[0] - маг защ.
                    // Для простоты и надежности возьмем из последней записи актуальный массив.
                    Resistance = g.OrderByDescending(x => x.RecordedAt).Select(x => x.Resistance).FirstOrDefault(),
                    Attack = g.Max(x => x.Attack),
                    Armor = g.Max(x => x.Armor),
                    AttackSpeed = g.Max(x => x.AttackSpeed),
                    RunSpeed = g.Max(x => x.RunSpeed),
                    AttackDegree = g.Max(x => x.AttackDegree),
                    DefendDegree = g.Max(x => x.DefendDegree),
                    CritRate = g.Max(x => x.CritRate),
                    DamageReduce = g.Max(x => x.DamageReduce),
                    Prayspeed = g.Max(x => x.Prayspeed),
                    CritDamageBonus = g.Max(x => x.CritDamageBonus),
                    InvisibleDegree = g.Max(x => x.InvisibleDegree),
                    AntiInvisibleDegree = g.Max(x => x.AntiInvisibleDegree),
                    Vigour = g.Max(x => x.Vigour),
                    AntiDefenseDegree = g.Max(x => x.AntiDefenseDegree),
                    AntiResistanceDegree = g.Max(x => x.AntiResistanceDegree),
                    PeakGrade = g.Max(x => x.PeakGrade),
                    UpdatedAt = g.Max(x => x.RecordedAt)
                })
                .FirstOrDefaultAsync();

            if (maxProps != null)
                results.Add(maxProps);
        }

        return Ok(results);
    }
}
