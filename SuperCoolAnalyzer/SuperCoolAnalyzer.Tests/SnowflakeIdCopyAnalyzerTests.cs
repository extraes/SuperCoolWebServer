using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
        SuperCoolAnalyzer.SnowflakeIdCopyAnalyzer,
        Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace SuperCoolAnalyzer.Tests;

public class SnowflakeIdCopyAnalyzerTests
{
    private const string SnowflakeStubs = """
using System;
using SuperCoolWebServer.Models;

namespace SuperCoolWebServer.Models
{
    public interface ISnowflake
    {
        long Id { get; }
    }

    public abstract class SnowflakeObject : ISnowflake
    {
        public long Id { get; init; }
    }
}

""";

    [Fact]
    public async Task IdCopiedBetweenSnowflakeTypes_ReportsDiagnostic()
    {
        const string source = """
public class IdentityUser<TKey>
{
    public TKey Id { get; set; }
}

public sealed class AppUser : IdentityUser<long>, ISnowflake
{
}

public sealed class AuditLogEntry : SnowflakeObject { }

public static class Factory
{
    public static AuditLogEntry Create(AppUser user) =>
        new AuditLogEntry { Id = {|#0:user.Id|} };
}
""";

        await VerifyAsync(source, Diagnostic("AppUser", "AuditLogEntry", 0));
    }

    [Fact]
    public async Task IdCopiedToSameSnowflakeType_ReportsDiagnostic()
    {
        const string source = """
public sealed class Entity : SnowflakeObject { }

public static class Factory
{
    public static Entity Clone(Entity existing) =>
        new Entity { Id = {|#0:existing.Id|} };
}
""";

        await VerifyAsync(source, Diagnostic("Entity", "Entity", 0));
    }

    [Fact]
    public async Task IdCopiedThroughSingleAssignmentLocal_ReportsDiagnostic()
    {
        const string source = """
public sealed class AppUser : ISnowflake
{
    public long Id { get; init; }
}

public sealed class AuditLogEntry : SnowflakeObject { }

public static class Factory
{
    public static AuditLogEntry Create(AppUser user)
    {
        var copiedId = user.Id;
        return new AuditLogEntry { Id = {|#0:copiedId|} };
    }
}
""";

        await VerifyAsync(source, Diagnostic("AppUser", "AuditLogEntry", 0));
    }

    [Fact]
    public async Task ConditionalWithCopiedIdBranch_ReportsDiagnostic()
    {
        const string source = """
public sealed class AppUser : ISnowflake
{
    public long Id { get; init; }
}

public sealed class AuditLogEntry : SnowflakeObject { }

public static class Factory
{
    public static AuditLogEntry Create(AppUser user, bool reuse, long generatedId) =>
        new AuditLogEntry { Id = {|#0:reuse ? user.Id : generatedId|} };
}
""";

        await VerifyAsync(source, Diagnostic("AppUser", "AuditLogEntry", 0));
    }

    [Fact]
    public async Task IdCopiedInWithInitializer_ReportsDiagnostic()
    {
        const string source = """
public sealed record Entity : ISnowflake
{
    public long Id { get; init; }
}

public static class Factory
{
    public static Entity Copy(Entity template, Entity other) =>
        template with { Id = {|#0:other.Id|} };
}
""";

        await VerifyAsync(source, Diagnostic("Entity", "Entity", 0));
    }

    [Fact]
    public async Task RelationshipIdsDtoProjectionAndGeneratedIds_DoNotReportDiagnostics()
    {
        const string source = """
public sealed class AppUser : ISnowflake
{
    public long Id { get; init; }
    public long CreatedBy { get; init; }
}

public sealed class AuditLogEntry : SnowflakeObject
{
    public long? EntityId { get; init; }
    public long? UserId { get; init; }
}

public sealed class TransportUser
{
    public TransportUser(AppUser user) => Id = user.Id;
    public long Id { get; init; }
}

public sealed class OrdinaryObject
{
    public long Id { get; init; }
}

public static class Factory
{
    public static AppUser CreateUser(AppUser creator, long generatedId) => new AppUser
    {
        Id = generatedId,
        CreatedBy = creator.Id
    };

    public static AuditLogEntry CreateAudit(AppUser user, long generatedId) => new AuditLogEntry
    {
        Id = generatedId,
        EntityId = user.Id,
        UserId = user.Id
    };

    public static AuditLogEntry CreateFromOrdinary(OrdinaryObject source) =>
        new AuditLogEntry { Id = source.Id };

    public static TransportUser Project(AppUser user) => new TransportUser(user);
}
""";

        await VerifyAsync(source);
    }

    [Fact]
    public async Task GeneratedIdThroughLocal_DoesNotReportDiagnostic()
    {
        const string source = """
public sealed class AuditLogEntry : SnowflakeObject { }

public static class Factory
{
    public static AuditLogEntry Create(long generatedId)
    {
        var id = generatedId;
        return new AuditLogEntry { Id = id };
    }
}
""";

        await VerifyAsync(source);
    }

    [Fact]
    public async Task ReassignedLocal_IsOutsideTrackingBoundary()
    {
        const string source = """
public sealed class AppUser : ISnowflake
{
    public long Id { get; init; }
}

public sealed class AuditLogEntry : SnowflakeObject { }

public static class Factory
{
    public static AuditLogEntry Create(AppUser user, long generatedId)
    {
        var id = user.Id;
        id = generatedId;
        return new AuditLogEntry { Id = id };
    }
}
""";

        await VerifyAsync(source);
    }

    private static DiagnosticResult Diagnostic(string sourceType, string targetType, int location) =>
        Verifier.Diagnostic(SnowflakeIdCopyAnalyzer.DiagnosticId)
            .WithLocation(location)
            .WithArguments(sourceType, targetType);

    private static Task VerifyAsync(string source, params DiagnosticResult[] expected) =>
        Verifier.VerifyAnalyzerAsync(SnowflakeStubs + source, expected);
}
