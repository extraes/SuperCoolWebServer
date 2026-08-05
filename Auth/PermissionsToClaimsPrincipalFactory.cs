using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SuperCoolWebServer.Models;

namespace SuperCoolWebServer.Auth;

public sealed class PermissionsToClaimsPrincipalFactory(
    UserManager<SuperCoolUser> userManager,
    RoleManager<IdentityRole<long>> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<SuperCoolUser, IdentityRole<long>>(userManager, roleManager, options)
{
    public const string PERMISSION_CLAIM_TYPE = nameof(SuperCoolUser.Permissions);

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(SuperCoolUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(
            PERMISSION_CLAIM_TYPE,
            ((long)user.Permissions).ToString()));

        return identity;
    }
}