namespace SuperCoolWebServer.Models;

public interface ISnowflake : IEquatable<SnowflakeObject>, IComparable<SnowflakeObject>
{
    public long Id { get; }
    public DateTimeOffset CreatedAt { get; }
}