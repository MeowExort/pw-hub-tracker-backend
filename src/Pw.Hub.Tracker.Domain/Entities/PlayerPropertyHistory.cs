namespace Pw.Hub.Tracker.Domain.Entities;

public class PlayerPropertyHistory
{
    public long Id { get; set; }
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
    public long Attack { get; set; }
    public long Armor { get; set; }
    public int AttackSpeed { get; set; }
    public double RunSpeed { get; set; }
    public int AttackDegree { get; set; }
    public int DefendDegree { get; set; }
    public int CritRate { get; set; }
    public int DamageReduce { get; set; }
    public int Prayspeed { get; set; }
    public int CritDamageBonus { get; set; }
    public int InvisibleDegree { get; set; }
    public int AntiInvisibleDegree { get; set; }
    public long Vigour { get; set; }
    public int AntiDefenseDegree { get; set; }
    public int AntiResistanceDegree { get; set; }
    public int PeakGrade { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
