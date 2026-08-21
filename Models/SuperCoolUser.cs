using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Snowflakes;

namespace SuperCoolWebServer.Models;

// The cloudflare client lib already uses the name "User"
// also the name is funny.
public class SuperCoolUser : IdentityUser<long>, ISnowflake
{
    public required long CreatedBy { get; init; }
    public Permissions Permissions { get; set; }
    
    
    
    public DateTimeOffset CreatedAt =>
        SnowflakeObject.Epoch.AddMilliseconds(Id >> 9);
    
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