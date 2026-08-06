using SuperCoolWebServer.Models;

namespace SuperCoolWebServer.Auth;

public static class PermissionsValidator
{
    private static readonly Permissions ValidPermissionsMask;
    static PermissionsValidator()
    {
        foreach (var permission in Enum.GetValues<Permissions>())
        {
            if (permission is Permissions.Administrator)
                continue;
            
            ValidPermissionsMask |= permission;
        }
    }
    
    public static Permissions MakeValid(Permissions permissions)
    {
        if (permissions is Permissions.Administrator)
            return permissions;
        
        return ValidPermissionsMask & permissions;
    }
}