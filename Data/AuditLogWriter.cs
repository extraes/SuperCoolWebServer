using System.Security.Claims;
using System.Text.Json;
using Snowflakes;
using SuperCoolWebServer.Models;

namespace SuperCoolWebServer.Data;

public sealed class AuditLogWriter(
    DataContext db,
    SnowflakeGenerator<long> snowflakeGenerator)
{
    public async Task WriteAsync(HttpContext httpContext,
        long? actorUserId,
        string action,
        string? entityType = null,
        long? entityId = null,
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        actorUserId ??= GetUserId(httpContext.User);
        
        string jsonStr = JsonSerializer.Serialize(new
        {
            RequestId = httpContext.TraceIdentifier,
            Data = details,
        });
        
        if (jsonStr.Length > AuditLogEntry.DETAILS_MAX_LENGTH)
        {
            var originalJson = jsonStr;
            int shortenBy = 128;
            while (jsonStr.Length > AuditLogEntry.DETAILS_MAX_LENGTH)
            {
                jsonStr = JsonSerializer.Serialize(new
                {
                    RequestId = httpContext.TraceIdentifier,
                    Data = new
                    {
                        TruncatedData = originalJson[..(AuditLogEntry.DETAILS_MAX_LENGTH - shortenBy)],
                    }
                }); 
                
                shortenBy += 128;
            }
        }
        
        var entry = new AuditLogEntry
        {
            Id = snowflakeGenerator.NewSnowflake(),
            UserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DetailsJson = jsonStr,
        };

        await db.AuditLogEntries.AddAsync(entry, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static long? GetUserId(ClaimsPrincipal principal)
    {
        var id = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(id, out var userId) ? userId : null;
    }
}
