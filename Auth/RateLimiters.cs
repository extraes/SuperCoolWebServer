using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Extensions;

namespace SuperCoolWebServer.Auth;

public static class RateLimiters
{
    // Not super strict but more limited than the global 100rpm
    public const string FIXED = "Fixed";
    
    // Pretty strict, only 10 rpm
    public const string STRICT = "Strict";
    
    // Only one request at a time!
    public const string CONCURRENT = "ConcurrentE";

    private static string GetUserString(HttpContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(ctx.User.Identity?.Name))
            return ctx.User.Identity.Name;
        if (ctx.Request.Headers.TryGetValue("cf-connecting-ip", out var headerIp)
            && !string.IsNullOrWhiteSpace(headerIp.ToString()))
            return headerIp.ToString();
        else
            return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
    
    public static void SetupRateLimiters(WebApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter(options =>
        {
            // Soft-ish 100 rpm limit by IP 
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            {
                string ipAddr = "unknown";
                if (ctx.Request.Headers.TryGetValue("cf-connecting-ip", out var headerIp)
                    && !string.IsNullOrWhiteSpace(headerIp.ToString()))
                    ipAddr = headerIp.ToString();
                else if (ctx.Connection.RemoteIpAddress is not null)
                    ipAddr = ctx.Connection.RemoteIpAddress.ToString();
                    
                return RateLimitPartition.GetFixedWindowLimiter(ipAddr, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 5
                });
            });

            // Added on a per-controller/per-endpoint basis
            options.AddPolicy(FIXED, ctx =>
            {
                string userStr = GetUserString(ctx);

                // 20 rpm
                return RateLimitPartition.GetFixedWindowLimiter(userStr, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 5,
                });
            });

            options.AddPolicy(STRICT, ctx =>
            {
                string userStr = GetUserString(ctx);

                // 20 req per 2 min -> soft 10rpm
                return RateLimitPartition.GetSlidingWindowLimiter(userStr, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(2),
                    SegmentsPerWindow = 4,
                    QueueLimit = 2,
                });
            });
                
            options.AddPolicy(CONCURRENT, ctx =>
            {
                var endpoint = ctx.GetEndpoint();
                string userStr = GetUserString(ctx);
                
                if (endpoint is null)
                    Logger.Put($"No endpoint was found for {userStr}'s request to {ctx.Request.GetDisplayUrl()}");

                string partitionKey = userStr + endpoint?.DisplayName;
                return RateLimitPartition.GetConcurrencyLimiter(partitionKey, _ => new ConcurrencyLimiterOptions());
            });
        });
    }
}