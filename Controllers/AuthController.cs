using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Snowflakes;
using SuperCoolWebServer.Auth;
using SuperCoolWebServer.Data;
using SuperCoolWebServer.Models;

namespace SuperCoolWebServer.Controllers;

[AutoValidateAntiforgeryToken]
[Route("auth/[action]")]
[EnableRateLimiting(RateLimiters.STRICT)]
public class AuthController : Controller
{
    [HttpPost]
    [ActionName("login")]
    [Consumes("application/json")]
    public async Task<IActionResult> Login(
        [FromServices] SignInManager<SuperCoolUser> signInManager,
        [FromServices] AuditLogWriter auditLog,
        [FromServices] DataContext db,
        [FromBody] AuthModels.LoginRequest loginReq)
    {
        var result = await signInManager.PasswordSignInAsync(
            loginReq.Username,
            loginReq.Password,
            isPersistent: true,
            lockoutOnFailure: false);
        
        
        var user = result.Succeeded
            ? await signInManager.UserManager.FindByNameAsync(loginReq.Username)
            : null;

        try
        {
            await auditLog.WriteAsync(
                HttpContext,
                user?.Id,
                result.Succeeded
                    ? AuditLogStrings.Actions.AUTH_LOGIN_SUCCEEDED
                    : AuditLogStrings.Actions.AUTH_LOGIN_FAILED, AuditLogStrings.Entities.USER,
                user?.Id,
                new { Username = loginReq.Username });
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to save sign-in log for {loginReq.Username} (ID {user?.Id})", ex);
        }
        

        return result.Succeeded ? Ok() : Unauthorized("Invalid username or password.");
    }
    
    [HttpPost]
    [ActionName("changePassword")]
    [Consumes("application/json")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        [FromServices] UserManager<SuperCoolUser> userManager,
        [FromServices] AuditLogWriter auditLog,
        [FromServices] DataContext db,
        [FromBody] AuthModels.ChangePasswordRequest passReq)
    {
        await using var txn = await db.Database.BeginTransactionAsync();
        
        var caller = await userManager.GetUserAsync(HttpContext.User);
        if (caller is null)
            return Unauthorized();
        
        var result = await userManager.ChangePasswordAsync(caller, passReq.CurrentPassword, passReq.NewPassword);
        
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        await auditLog.WriteAsync(
            HttpContext, null,
            AuditLogStrings.Actions.USER_PASSWORD_CHANGED,
            AuditLogStrings.Entities.USER,
            caller!.Id);
        
        await txn.CommitAsync();
        return Ok();
    }
    
    [HttpPost]
    [ActionName("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(
        [FromServices] SignInManager<SuperCoolUser> signInManager,
        [FromServices] AuditLogWriter auditLog)
    {
        var user = await signInManager.UserManager.GetUserAsync(HttpContext.User);
        await signInManager.SignOutAsync();
        await auditLog.WriteAsync(
            HttpContext,
            user?.Id, AuditLogStrings.Actions.AUTH_LOGOUT, AuditLogStrings.Entities.USER, user?.Id);
        return Ok();
    }
}
