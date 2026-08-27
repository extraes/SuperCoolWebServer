using Snowflakes;

namespace SuperCoolWebServer.Models;

public abstract class SnowflakeObject : ISnowflake
{
    public static readonly DateTime Epoch = new DateTime(2026, 1, 1,  0, 0, 0, DateTimeKind.Utc);
    
    public required long Id { get; init; }
    public DateTimeOffset CreatedAt =>
        Epoch.AddMilliseconds(Id >> 9);
    
    public bool Equals(SnowflakeObject? other)
    {
        if (other is null) return false;
        return Id == other.Id;
    }

    public int CompareTo(SnowflakeObject? other)
    {
        if (other is null)
            return 1;
        
        if (Id < other.Id)
            return -1;
        return this.Id > other.Id ? 1 : 0;
    }
}