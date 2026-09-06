using System.Security.Claims;
using System.Text.Json;
using Snowflakes;
using SuperCoolWebServer.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace SuperCoolWebServer.Data;

public sealed class AuditLogWriter(
    DataContext db,
    SnowflakeGenerator<long> snowflakeGenerator)
{
    /// <summary>
    /// Writes an entry into the DB's audit log table.
    /// <para>If the caller (YOU) touches the DB, make sure you use <see cref="DatabaseFacade.BeginTransactionAsync"/></para>
    /// <br/> (And make sure you call <see cref="DatabaseFacade.CommitTransactionAsync"/> once you're done with everything) 
    /// </summary>
    /// <param name="httpContext">The context from which an action was taken.</param>
    /// <param name="actorUserId">The ID of the user taking the action. If it's null, the ID will be gathered from <br/>
    ///                           the <paramref name="httpContext"/> instead.</param>
    /// <param name="action">A string representing what was done.
    ///                      Typically taken from <see cref="AuditLogStrings"/>.</param>
    /// <param name="entityType">A string representing what was made/deleted/changed.
    ///                          Typically taken from <see cref="AuditLogStrings"/>.</param>
    /// <param name="entityId">The snowflake ID of the thing made/deleted/changed.</param>
    /// <param name="details">An object (usually anonymous type) containing details of what happened.</param>
    /// <param name="cancellationToken">A cancellation token used to cancel this operation.</param>
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
