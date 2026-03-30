namespace Pw.Hub.Tracker.Domain.Entities;

public class Player
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Cls { get; set; }
    public int Gender { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
