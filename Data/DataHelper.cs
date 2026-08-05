using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Snowflakes;
using SuperCoolWebServer.Models;

namespace SuperCoolWebServer.Data;

public static class DataHelper
{
    public static async Task Initialize(UserManager<SuperCoolUser> userManager,
        SnowflakeGenerator<long> snowflakeGen)
    {
        if (await userManager.Users.AnyAsync())
        {
            return;
        }
        
        var passwordString = CreateRandomPassword();

        Logger.Put($"Creating admin user w/ Username 'Admin' and password '{passwordString}'");
        var result = await userManager.CreateAsync(new SuperCoolUser()
        {
            Id = snowflakeGen.NewSnowflake(),
            Permissions = Permissions.Administrator,
            UserName = "Admin",
        }, passwordString);
        
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(error => error.Code + ": " + error.Description);
            var errorStr = string.Join("\n\t", errors);

            throw new InvalidOperationException($"Could not create initial administrator:\n\t{errorStr}");
        }
        
        Logger.Put("Created administrator user after initializing database");
            
        
    }
    
    public static string CreateRandomPassword()
    {
        string alphabet = "abcdefghijklmnopqrstuvwxyz1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ!@#$%^&*()_+-={}|:;<>";
        const int PASSWORD_LENGTH = 24;
        Span<char> password = stackalloc char[PASSWORD_LENGTH];
        string passwordString;
        do
        {
            RandomNumberGenerator.GetItems(alphabet, password);
            passwordString = new string(password);
        }
        while (passwordString.All(char.IsLetterOrDigit));

        return passwordString;
    }
}