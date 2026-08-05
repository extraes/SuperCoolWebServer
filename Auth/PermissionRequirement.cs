using Microsoft.AspNetCore.Authorization;
using SuperCoolWebServer.Models;

namespace SuperCoolWebServer.Auth;

public sealed record PermissionRequirement(Permissions Permission) : IAuthorizationRequirement;