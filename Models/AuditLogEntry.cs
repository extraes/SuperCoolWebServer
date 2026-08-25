using System.ComponentModel.DataAnnotations;

namespace SuperCoolWebServer.Models;

public class AuditLogEntry : SnowflakeObject
{
    public const int DETAILS_MAX_LENGTH = 1024 * 16;
    
    [MaxLength(64)] public required string Action { get; init; }
    
    [MaxLength(128)] public string? EntityType { get; init; }
    public long? EntityId { get; init; }
    
    public long? UserId { get; init; }
    // 16KB is probably more than I need, but it's a good thing to have and not need than need and not have. 
    [MaxLength(DETAILS_MAX_LENGTH)] public string? DetailsJson { get; init; }
}