using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
        SuperCoolAnalyzer.IdentityUserResponseAnalyzer,
        Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace SuperCoolAnalyzer.Tests;

public class IdentityUserResponseAnalyzerTests
{
    private const string FrameworkStubs = """
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.AspNetCore.Identity
{
    public class IdentityUser : IdentityUser<string> { }
    public class IdentityUser<TKey> { }
}

namespace Microsoft.AspNetCore.Mvc
{
    public interface IActionResult { }

    public abstract class ActionResult : IActionResult { }

    public class ActionResult<T> : ActionResult
    {
        public ActionResult(T value) { }
    }

    public class ObjectResult : ActionResult
    {
        public ObjectResult(object value) { }
    }

    public sealed class OkObjectResult : ObjectResult
    {
        public OkObjectResult(object value) : base(value) { }
    }

    public sealed class JsonResult : ActionResult
    {
        public JsonResult(object data) { }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public sealed class NonActionAttribute : Attribute { }

    public abstract class ControllerBase
    {
        protected OkObjectResult Ok(object value) => new OkObjectResult(value);
        protected ObjectResult StatusCode(int statusCode, object value) => new ObjectResult(value);
        protected JsonResult Json(object data) => new JsonResult(data);
    }
}

""";

    [Fact]
    public async Task DirectIdentityPayload_ReportsDiagnostic()
    {
        const string source = """
public sealed class AppUser : IdentityUser<long> { }

public sealed class UsersController : ControllerBase
{
    public IActionResult Get(AppUser user) => Ok({|#0:user|});
}
""";

        await VerifyAsync(source, Diagnostic("AppUser", 0));
    }

    [Fact]
    public async Task IdentityNestedInAnonymousResponse_ReportsDiagnostic()
    {
        const string source = """
public sealed class AppUser : IdentityUser<long> { }

public sealed class UsersController : ControllerBase
{
    public IActionResult Create(AppUser user) => Ok(new { User = {|#0:user|}, Message = "created" });
}
""";

        await VerifyAsync(source, Diagnostic("AppUser", 0));
    }

    [Fact]
    public async Task IdentityHiddenBehindObjectLocal_ReportsDiagnostic()
    {
        const string source = """
public sealed class AppUser : IdentityUser<long> { }

public sealed class UsersController : ControllerBase
{
    public IActionResult Get(AppUser user)
    {
        object payload = {|#0:user|};
        return Ok(payload);
    }
}
""";

        await VerifyAsync(source, Diagnostic("AppUser", 0));
    }

    [Fact]
    public async Task IdentityInObjectTypedCollection_ReportsDiagnostic()
    {
        const string source = """
public sealed class AppUser : IdentityUser<long> { }

public sealed class UsersController : ControllerBase
{
    public IActionResult Get(AppUser user)
    {
        var payload = new List<object> { {|#0:user|} };
        return Ok(payload);
    }
}
""";

        await VerifyAsync(source, Diagnostic("AppUser", 0));
    }

    [Fact]
    public async Task IdentityAddedToObjectTypedCollection_ReportsDiagnostic()
    {
        const string source = """
public sealed class AppUser : IdentityUser<long> { }

public sealed class UsersController : ControllerBase
{
    public IActionResult Get(AppUser user)
    {
        var payload = new List<object>();
        payload.Add({|#0:user|});
        return Ok(payload);
    }
}
""";

        await VerifyAsync(source, Diagnostic("AppUser", 0));
    }

    [Fact]
    public async Task UnsafeTypedContract_ReportsOnceAtReturnType()
    {
        const string source = """
public sealed class AppUser : IdentityUser<long> { }

public sealed class UsersController : ControllerBase
{
    public {|#0:ActionResult<AppUser>|} Get(AppUser user) => new ActionResult<AppUser>(user);
}
""";

        await VerifyAsync(source, Diagnostic("AppUser", 0));
    }

    [Fact]
    public async Task ExplicitObjectResult_ReportsDiagnostic()
    {
        const string source = """
public sealed class AppUser : IdentityUser<long> { }

public sealed class UsersController : ControllerBase
{
    public IActionResult Get(AppUser user) => new ObjectResult({|#0:user|});
}
""";

        await VerifyAsync(source, Diagnostic("AppUser", 0));
    }

    [Fact]
    public async Task SafeDtoMapping_DoesNotReportDiagnostic()
    {
        const string source = """
public sealed class AppUser : IdentityUser<long>
{
    public string UserName { get; set; }
}

public sealed class UserDto
{
    public UserDto(AppUser user) => UserName = user.UserName;
    public string UserName { get; }
}

public sealed class UsersController : ControllerBase
{
    public IActionResult Get(AppUser user) => Ok(new UserDto(user));
}
""";

        await VerifyAsync(source);
    }

    [Fact]
    public async Task NonActionMethod_DoesNotReportDiagnostic()
    {
        const string source = """
public sealed class AppUser : IdentityUser<long> { }

public sealed class UsersController : ControllerBase
{
    [NonAction]
    public IActionResult Helper(AppUser user) => Ok(user);
}
""";

        await VerifyAsync(source);
    }

    [Fact]
    public async Task NonControllerMethod_DoesNotReportDiagnostic()
    {
        const string source = """
public sealed class AppUser : IdentityUser<long> { }

public sealed class UserService
{
    public IActionResult Get(AppUser user) => new ObjectResult(user);
}
""";

        await VerifyAsync(source);
    }

    [Fact]
    public async Task SafeTaskOfActionResult_DoesNotReportDiagnostic()
    {
        const string source = """
public sealed class AppUser : IdentityUser<long> { }
public sealed class UserDto { public string Name { get; set; } }

public sealed class UsersController : ControllerBase
{
    public async Task<IActionResult> Get(AppUser user)
    {
        await Task.Yield();
        return Ok(new UserDto());
    }
}
""";

        await VerifyAsync(source);
    }

    [Fact]
    public async Task TaskOfActionResultPayload_ReportsDiagnostic()
    {
        const string source = """
public sealed class AppUser : IdentityUser<long> { }

public sealed class UsersController : ControllerBase
{
    public Task<IActionResult> Get(AppUser user) =>
        Task.FromResult<IActionResult>(Ok({|#0:user|}));
}
""";

        await VerifyAsync(source, Diagnostic("AppUser", 0));
    }

    private static DiagnosticResult Diagnostic(string exposedType, int location) =>
        Verifier.Diagnostic(IdentityUserResponseAnalyzer.DiagnosticId)
            .WithLocation(location)
            .WithArguments(exposedType);

    private static Task VerifyAsync(string source, params DiagnosticResult[] expected) =>
        Verifier.VerifyAnalyzerAsync(FrameworkStubs + source, expected);
}
