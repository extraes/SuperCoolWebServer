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
        [FromServices] UserManager<SuperCoolUser> userManager)
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
            UserName = creationReq.Username,
            Id = snowflakeGenerator.NewSnowflake(),
            Permissions = validPermissions
        };
        var passwordStr = DataHelper.CreateRandomPassword();
        var result = await userManager.CreateAsync(newUser, passwordStr);
        if (result.Succeeded)
            return Ok(new
            {
                user = newUser,
                password = passwordStr,
                message = "Please change your password when you log in :)"
            }); 
        
        return StatusCode(500, result.Errors);
    }
    
    [HttpDelete]
    [ActionName("delete")]
    [Authorize(Policy = nameof(Permissions.ManageUsers))]
    public async Task<IActionResult> Remove(
        long id,
        [FromServices] UserManager<SuperCoolUser> userManager)
    {
        var targetUser = await userManager.FindByIdAsync(id.ToString());
        if (targetUser is null)
        {
            return NotFound($"No such user with id {id} exists.");
        }
        
        var result = await userManager.DeleteAsync(targetUser);
        if (result.Succeeded)
            return Ok(); 
        
        return StatusCode(500, result.Errors);
    }
    
    [HttpPost]
    [ActionName("changeUserPassword")]
    [Consumes("application/json")]
    [Authorize(policy: nameof(Permissions.ManageUsers))]
    public async Task<IActionResult> ChangeUserPassword(
        [FromBody] AuthModels.SetUserPasswordRequest setReq,
        [FromServices] UserManager<SuperCoolUser> userManager)
    {
        var target = await userManager.FindByIdAsync(setReq.TargetId.ToString());
        if (target is null)
        {
            return NotFound($"User with id {setReq.TargetId} does not exist.");
        }
        
        var passwordChangeToken =  await userManager.GeneratePasswordResetTokenAsync(target);
        var result = await userManager.ChangePasswordAsync(target!, passwordChangeToken, setReq.NewPassword);
        
        return result.Succeeded
            ? Ok()
            : BadRequest(result.Errors);
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
        
        return Ok(existingUser);
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
        
        return Ok(existingUser);
    }
}