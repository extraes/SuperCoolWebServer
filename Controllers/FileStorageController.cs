using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using SuperCoolWebServer.Auth;
using SuperCoolWebServer.Data;
using SuperCoolWebServer.Models;
using Xabe.FFmpeg;

namespace SuperCoolWebServer.Controllers;

[Route("api/files/[action]/{file}")]
public partial class FileStorageController : Controller
{
    static readonly Regex portRegex = PortRegex();
    const int MB_SIZE = 1024 * 1024;
    static string BaseDirectory => Path.GetFullPath(Config.values.filestoreDir);
    static ConditionalWeakTable<string, byte[]> cachedFiles = new();
    static ConditionalWeakTable<string, IMediaInfo> cachedProbes = new();

    static void EnsureDirectory()
    {
        System.IO.Directory.CreateDirectory(BaseDirectory);
    }

    [HttpGet]
    [ActionName("query")]
    public IActionResult QueryFile(string file)
    {
        FileInfo finf = new(Path.Combine(BaseDirectory, file));
        if (!finf.Exists) return NotFound();
        
        return Content(finf.Length.ToString());
    }

    [HttpGet]
    [ActionName("list")]
    [Authorize(Policy = nameof(Permissions.ListFiles))]
    [AutoValidateAntiforgeryToken]
    public IActionResult ListFiles(
        [Description("Used as a wildcard-supporting search string. Use an asterisk to get all files (or an empty string, as it's replaced w/ an asterisk).\n" +
                     "If your query isn't surrounded in asterisks, it will be, so that your search returns files with your string inside it.")]
        string file,
        bool oldestFirst = false,
        int offset = 0,
        int limit = 100)
    {
        if (limit > 100 || limit < 1)
            return BadRequest("Limit must be between 1 and 100");
        
        const string WILDCARD_CHARS = "?*"; // this is all GetFiles supports lol
        if (Path.GetInvalidFileNameChars().Except(WILDCARD_CHARS).Any(file.Contains)
            || file.Contains('\\') || file.Contains('/'))
            return BadRequest("Must be a valid string");
        
        file = string.IsNullOrWhiteSpace(file) ? "*" : $"*{file.Trim('*')}*";

        if (!Directory.Exists(BaseDirectory))
            return Json(Array.Empty<string>());
        
        FileInfo[] fileList = new DirectoryInfo(BaseDirectory).GetFiles(file);
        fileList = (oldestFirst
            ? fileList.OrderBy(f => f.LastWriteTime)
            : fileList.OrderByDescending(f => f.LastWriteTime)).ToArray();

        var resultList = fileList.Skip(offset).Take(limit);
        
        return Json(new {
            Items = resultList.Select(f => f.Name).ToArray(),
            Total = fileList.Length
        })
        ;
    }

    [HttpGet]
    [ActionName("exists")]
    public IActionResult Exists(string file)
    {
        FileInfo finf = new(Path.Combine(BaseDirectory, file));
        if (!finf.Exists) return NotFound();

        return Ok();
    }

    // literally just gets a page and displays it as HTML.
    // stupid simple and probably a bad idea. but #WeBall, and as of when this is implemented, uploading files requires
    // an account with the permission to do so, so this shouldn't be abused (fingers crossed)
    [HttpGet]
    [ActionName("site")]
    public async Task<IActionResult> Site(string file)
    {
        if (string.IsNullOrEmpty(file)
            || file.Any(c => c is '/' or '\\')
            || Path.GetExtension(file) != ".html")
            return BadRequest();

        FileInfo finf = new(Path.Combine(BaseDirectory, file));
        if (!finf.Exists) return NotFound();
        
        return Content(await finf.OpenText().ReadToEndAsync(), "text/html");
    }

    [HttpGet]
    [ActionName("dl")]
    [EnableRateLimiting(RateLimiters.STRICT)]
    public async Task<IActionResult> Download(string file, bool redirDisc = true)
    {
        if (!Request.Headers.TryGetValue("cf-connecting-ip", out var ip))
            ip = Request.HttpContext.Connection.RemoteIpAddress?.ToString();
        bool isDiscord = Request.Headers.TryGetValue("User-Agent", out var ua) && ua.ToString().Contains("Discord");

        Logger.Put($"IP {ip} requested file {file}", LogType.Debug);

        if (string.IsNullOrEmpty(file) || file.Any(c => c == '/' || c == '\\'))
            return BadRequest();

        FileInfo finf = new(Path.Combine(BaseDirectory, file));
        if (!finf.Exists) return NotFound();

        string mime = Path.GetExtension(file) switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".tif" => "image/tiff",
            ".tiff" => "image/tiff",
            ".avif" => "image/avif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mkv" => "video/x-matroska",
            _ => "application/octet-stream",
        };

        // dont need to redirect discord because it previews <100mb files just fine normally
        if (finf.Length < 20 * MB_SIZE) // dont cache files larger than 20mb
        {
            if (!cachedFiles.TryGetValue(file, out byte[]? bytes))
            {
                bytes = await System.IO.File.ReadAllBytesAsync(finf.FullName);
                cachedFiles.Add(file, bytes);
            }

            return File(bytes, mime/*, finf.Name*/);
        }

        if (!mime.Contains("video") || !isDiscord || !redirDisc)
            return PhysicalFile(finf.FullName, mime, true);

        // default 16 by 9
        int width = 426;
        int height = 240;
        try
        {
            if (!cachedProbes.TryGetValue(file, out IMediaInfo? info))
            {
                info = await FFmpeg.GetMediaInfo(finf.FullName);
                cachedProbes.Add(file, info);
            }

            var vidStream = info.VideoStreams.FirstOrDefault();
            if (vidStream is not null)
            {
                width = vidStream.Width;
                height = vidStream.Height;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Exception while probing file {file} for {ip} - {ex}");
        }

        foreach (var item in Request.Headers)
        {
            Logger.Put($"{item.Key}: {item.Value}", LogType.Debug);
        }

        string thumbName = finf.Name.Replace(finf.Extension, ".jpg");
        string thumbFullName = finf.FullName.Replace(finf.Extension, ".jpg");
        string displayUrl = portRegex.Replace(Request.GetDisplayUrl(), "");
        string thumbUrl = System.IO.File.Exists(thumbFullName) ? displayUrl.Replace(file, thumbName) : Config.values.filestoreDefaultThumbnail;

        //todone: use parsequerystring
        var queryStr = HttpUtility.ParseQueryString(Request.QueryString.Value ?? "");
        queryStr[nameof(redirDisc)] = "false";
        string newUrl = Request.GetDisplayUrl().Split('?')[0] + "?" + queryStr.ToString();

        //string newUrl = displayUrl;
        //// theres almost certainly a better way to do this but i dont care
        //if (newUrl.Contains(nameof(redirDisc)))
        //    newUrl = newUrl.Replace($"{nameof(redirDisc)}=false", $"{nameof(redirDisc)}=true");
        //else if (newUrl.Contains('?'))
        //    newUrl += $"&{nameof(redirDisc)}=false";
        //else
        //    newUrl += $"?{nameof(redirDisc)}=false";


        string discordHtml = string.Format(DiscordFormat.LARGE_VIDEO_FORMAT, thumbUrl, newUrl, width, height);
        //Response.ContentType = "text/html";

        return Content(discordHtml, "text/html");
    }

    [HttpPut]
    [ActionName("upload")]
    [Consumes("application/octet-stream", IsOptional = true)]
    [RequestSizeLimit(1024 * MB_SIZE)]
    [Authorize(Policy = nameof(Permissions.UploadFiles))]
    public async Task<IActionResult> Upload(
        [FromServices] UserManager<SuperCoolUser> userManager,
        [FromServices] AuditLogWriter auditLog,
        [FromBody] Stream fileStream,
        string file,
        string auth,
        bool overwrite = false)
    {
        if (!Request.Headers.TryGetValue("cf-connecting-ip", out var ip))
            ip = Request.HttpContext.Connection.RemoteIpAddress?.ToString();

        if (string.IsNullOrEmpty(file) || file.Any(c => c == '/' || c == '\\'))
            return BadRequest();
        
        var user = await userManager.GetUserAsync(HttpContext.User);
        if (user is null)
            return Unauthorized();

        EnsureDirectory();

        string path = Path.Combine(BaseDirectory, file);
        var existed = System.IO.File.Exists(path);
        if (existed && !overwrite)
            return StatusCode(409);

        Logger.Put($"IP {ip} is uploading file {file}", LogType.Debug);

        using FileStream fs = System.IO.File.Create(path);
        
        await fileStream.CopyToAsync(fs);

        Logger.Put($"IP {ip} uploaded {file} that is {fs.Length / 1024} KB long", LogType.Debug);
        try
        {

            await auditLog.WriteAsync(
                HttpContext,
                actorUserId: user.Id,
                action: existed
                    ? AuditLogStrings.Actions.FILE_OVERWRITTEN
                    : AuditLogStrings.Actions.UPLOADED_FILE_MVC, entityType: AuditLogStrings.Entities.FILE, details: new
                {
                    Filename = file,
                    SizeBytes = fs.Length,
                    UploadMethod = "mvc",
                });
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to save a log for {user.UserName} (ID {user.Id}) uploading a file.", ex);
        }
        

        string url = Request.GetDisplayUrl().Split('?')[0];
        url = portRegex.Replace(url, "");

        return Created(url.Replace("upload", "dl"), null);
    }
    
    [GeneratedRegex("\\:\\d{1,5}")]
    private static partial Regex PortRegex();
}
