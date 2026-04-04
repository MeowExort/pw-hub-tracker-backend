namespace Pw.Hub.Tracker.Domain.Entities;

public class PlayerMaxStats
{
    public long PlayerId { get; set; }
    public string Server { get; set; } = null!;
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
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
