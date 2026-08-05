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
        [FromBody] AuthModels.LoginRequest loginReq,
        [FromServices] SignInManager<SuperCoolUser> signInManager)
    {
        var result = await signInManager.PasswordSignInAsync(
            loginReq.Username,
            loginReq.Password,
            isPersistent: true,
            lockoutOnFailure: false);
        
        
        return result.Succeeded
            ? Ok()
            : Unauthorized("Invalid username or password.");
    }
    
    [HttpPost]
    [ActionName("changePassword")]
    [Consumes("application/json")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        [FromBody] AuthModels.ChangePasswordRequest passReq,
        [FromServices] UserManager<SuperCoolUser> userManager)
    {
        var caller = await userManager.GetUserAsync(HttpContext.User);
        var result = await userManager.ChangePasswordAsync(caller!, passReq.CurrentPassword, passReq.NewPassword);
        
        return result.Succeeded
            ? Ok()
            : BadRequest(result.Errors);
    }
    
    [HttpPost]
    [ActionName("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(
        [FromServices] SignInManager<SuperCoolUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Ok();
    }
}