using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SuperCoolWebServer.Models;

namespace SuperCoolWebServer.Auth;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var permStr = context.User.FindFirstValue(nameof(SuperCoolUser.Permissions));
        if (string.IsNullOrEmpty(permStr) || !long.TryParse(permStr, out long permissionsInteger))
        {
            context.Fail();
            return Task.CompletedTask;
        }

        var perms = (Permissions)permissionsInteger;
        
        if (perms.HasFlag(requirement.Permission))
            context.Succeed(requirement);
        
        return Task.CompletedTask;
    }
}