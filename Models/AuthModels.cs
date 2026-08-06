using System.Text.Json.Serialization;

namespace SuperCoolWebServer.Models;

public static class AuthModels
{
    public record LoginRequest(string Username, string Password);
    public record SetUserPasswordRequest(long TargetId, string NewPassword);
    public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    // Allows JS to serialize the permissions integer as a string from a BigInt
    public record UserCreationRequest(string Username,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] long PermissionsInteger)
    {
        [JsonIgnore] public Permissions Permissions => (Permissions)PermissionsInteger;
    }
}