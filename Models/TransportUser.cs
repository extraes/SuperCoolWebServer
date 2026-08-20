namespace SuperCoolWebServer.Models;

public class TransportUser(SuperCoolUser user)
{
        long Id { get; init; } = user.Id;
        string? UserName { get; init; } = user.UserName;
        string? NormalizedUserName { get; init; } = user.NormalizedUserName;
        long CreatedBy { get; init; } = user.CreatedBy;
        Permissions Permissions { get; init; } = user.Permissions;
}