using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Snowflakes;
using SuperCoolWebServer.Auth;
using SuperCoolWebServer.Data;
using SuperCoolWebServer.Models;

namespace SuperCoolWebServer.Controllers;

[AutoValidateAntiforgeryToken]
[Authorize]
[Route("users/[action]")]
public class UsersController : Controller
{
    [HttpPost]
    [ActionName("create")]
    [Consumes("application/json")]
    [Authorize(Policy = nameof(Permissions.ManageUsers))]
    public async Task<IActionResult> Create(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] AuthModels.UserCreationRequest creationReq,
        [FromServices] SnowflakeGenerator<long> snowflakeGenerator,
        [FromServices] UserManager<SuperCoolUser> userManager,
        [FromServices] AuditLogWriter auditLog)
    {
        var validPermissions = PermissionsValidator.MakeValid(creationReq.Permissions);
        var caller = await userManager.GetUserAsync(HttpContext.User);
        if (!caller!.Permissions.HasFlag(validPermissions)) // make sure given perms are less than what the user has
        {
            return Unauthorized($"Your permissions ({caller.Permissions}) are less than what you're trying to give that user.");
        }
        
        var existingUser = await userManager.FindByNameAsync(creationReq.Username);
        if (existingUser is not null)
        {
            return BadRequest($"A user with that username already exists.");
        }
        
        var newUser = new SuperCoolUser()
        {
            CreatedBy = caller.Id,
            UserName = creationReq.Username,
            Id = snowflakeGenerator.NewSnowflake(),
            Permissions = validPermissions
        };
        var passwordStr = DataHelper.CreateRandomPassword();
        var result = await userManager.CreateAsync(newUser, passwordStr);
        if (result.Succeeded)
        {
            await auditLog.WriteAsync(
                HttpContext,
                caller.Id,
                AuditLogStrings.Actions.USER_CREATED, AuditLogStrings.Entities.USER, newUser.Id, new
                {
                    Username = newUser.UserName,
                    Permissions = (long)newUser.Permissions,
                });
            return Ok(new
            {
                user = new TransportUser(newUser),
                password = passwordStr,
                message = "Please change your password when you log in :)"
            });
        }
        
        return StatusCode(500, result.Errors);
    }
    
    [HttpDelete]
    [ActionName("delete")]
    [Authorize(Policy = nameof(Permissions.ManageUsers))]
    public async Task<IActionResult> Remove(
        long id,
        [FromServices] UserManager<SuperCoolUser> userManager,
        [FromServices] AuditLogWriter auditLog)
    {
        var targetUser = await userManager.FindByIdAsync(id.ToString());
        if (targetUser is null)
        {
            return NotFound($"No such user with id {id} exists.");
        }
        
        var result = await userManager.DeleteAsync(targetUser);
        if (result.Succeeded)
        {
            await auditLog.WriteAsync(
                HttpContext, null,
                AuditLogStrings.Actions.USER_DELETED,
                AuditLogStrings.Entities.USER,
                targetUser.Id,
                details: new { Username = targetUser.UserName });
            return Ok();
        }
        
        return StatusCode(500, result.Errors);
    }
    
    [HttpPost]
    [ActionName("changeUserPassword")]
    [Consumes("application/json")]
    [Authorize(policy: nameof(Permissions.ManageUsers))]
    public async Task<IActionResult> ChangeUserPassword(
        [FromBody] AuthModels.SetUserPasswordRequest setReq,
        [FromServices] UserManager<SuperCoolUser> userManager,
        [FromServices] AuditLogWriter auditLog)
    {
        var target = await userManager.FindByIdAsync(setReq.TargetId.ToString());
        if (target is null)
        {
            return NotFound($"User with id {setReq.TargetId} does not exist.");
        }
        
        var passwordChangeToken =  await userManager.GeneratePasswordResetTokenAsync(target);
        var result = await userManager.ResetPasswordAsync(target, passwordChangeToken, setReq.NewPassword);
        
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        await auditLog.WriteAsync(
            HttpContext, null,
            AuditLogStrings.Actions.USER_PASSWORD_RESET_BY_ADMIN,
            AuditLogStrings.Entities.USER,
            target.Id,
            details: new { Username = target.UserName });
        return Ok();
    }
    
    [HttpGet]
    [ActionName("findById")]
    [Authorize]
    public async Task<IActionResult> FindById(
        long id,
        [FromServices] UserManager<SuperCoolUser> userManager)
    {
        var existingUser = await userManager.FindByIdAsync(id.ToString());
        if (existingUser is null)
        {
            return NotFound();
        }
        
        return Ok(new TransportUser(existingUser));
    }
    
    [HttpGet]
    [ActionName("findByName")]
    [Authorize]
    public async Task<IActionResult> FindByName(
        string username,
        [FromServices] UserManager<SuperCoolUser> userManager)
    {
        var existingUser = await userManager.FindByNameAsync(username);
        if (existingUser is null)
        {
            return NotFound();
        }
        
        return Ok(new TransportUser(existingUser));
    }
}
