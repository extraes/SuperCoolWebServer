using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Snowflakes;

namespace SuperCoolWebServer.Models;

// The cloudflare client lib already uses the name "User"
// also the name is funny.
public class SuperCoolUser : IdentityUser<long>
{
    public Permissions Permissions { get; set; }
}