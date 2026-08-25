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

        var entry = new AuditLogEntry
        {
            Id = snowflakeGenerator.NewSnowflake(),
            UserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DetailsJson = JsonSerializer.Serialize(new
            {
                RemoteIp = httpContext.Connection.RemoteIpAddress?.ToString(),
                RequestId = httpContext.TraceIdentifier,
                Data = details,
            }),
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
