namespace SuperCoolWebServer.Models;

public static class AuthModels
{
    public record LoginRequest(string Username, string Password);
    public record SetUserPasswordRequest(long TargetId, string NewPassword);
    public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    public record UserCreationRequest(string Username, Permissions Permissions);
}