namespace SuperCoolWebServer.Models;

public class TransportUser(SuperCoolUser user)
{
        public long Id { get; init; } = user.Id;
        public string? UserName { get; init; } = user.UserName;
        public string? NormalizedUserName { get; init; } = user.NormalizedUserName;
        public long CreatedBy { get; init; } = user.CreatedBy;
        public Permissions Permissions { get; init; } = user.Permissions;
}