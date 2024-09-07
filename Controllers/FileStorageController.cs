using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xabe.FFmpeg;

namespace SuperCoolWebServer.Controllers;

[Route("api/files/[action]/{file}")]
public partial class FileStorageController : Controller
{
    static readonly Regex portRegex = PortRegex();
    const int MB_SIZE = 1024 * 1024;
    static string Directory => Path.GetFullPath(Config.values.filestoreDir);
    static ConditionalWeakTable<string, byte[]> cachedFiles = new();
    static ConditionalWeakTable<string, IMediaInfo> cachedProbes = new();

    static void EnsureDirectory()
    {
        System.IO.Directory.CreateDirectory(Directory);
    }

    [HttpGet]
    [ActionName("query")]
    public IActionResult QueryFile(string file)
    {
        FileInfo finf = new(Path.Combine(Directory, file));
        if (!finf.Exists) return NotFound();


        return Content(finf.Length.ToString());
    }

    [HttpGet]
    [ActionName("exists")]
    public IActionResult Exists(string file)
    {
        FileInfo finf = new(Path.Combine(Directory, file));
        if (!finf.Exists) return NotFound();

        return Ok();
    }

    [HttpGet]
    [ActionName("dl")]
    public async Task<IActionResult> Download(string file, bool redirDisc = true)
    {
        if (!Request.Headers.TryGetValue("cf-connecting-ip", out var ip))
            ip = Request.HttpContext.Connection.RemoteIpAddress?.ToString();
        bool isDiscord = Request.Headers.TryGetValue("User-Agent", out var ua) && ua.ToString().Contains("Discord");

        Logger.Put($"IP {ip} requested file {file}", LogType.Debug);

        if (string.IsNullOrEmpty(file) || file.Any(c => c == '/' || c == '\\'))
            return BadRequest();

        FileInfo finf = new(Path.Combine(Directory, file));
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

        
        string newUrl = displayUrl;
        // theres almost certainly a better way to do this but i dont care
        if (newUrl.Contains(nameof(redirDisc)))
            newUrl = newUrl.Replace($"{nameof(redirDisc)}=false", $"{nameof(redirDisc)}=true");
        else if (newUrl.Contains('?'))
            newUrl += $"&{nameof(redirDisc)}=false";
        else
            newUrl += $"?{nameof(redirDisc)}=false";

        string discordHtml = string.Format(DiscordFormat.LARGE_VIDEO_FORMAT, thumbUrl, newUrl, width, height);
        //Response.ContentType = "text/html";
        
        return Ok(new HtmlString(discordHtml));
    }

    [HttpPut]
    [ActionName("upload")]
    [Consumes("application/octet-stream", IsOptional = true)]
    [RequestSizeLimit(1024 * MB_SIZE)]
    public async Task<IActionResult> Upload([FromBody] Stream fileStream, string file, string auth, bool overwrite = false)
    {
        if (!Request.Headers.TryGetValue("cf-connecting-ip", out var ip))
            ip = Request.HttpContext.Connection.RemoteIpAddress?.ToString();
        
        if (Config.values.filestoreAuth != auth)
            return Unauthorized();

        if (string.IsNullOrEmpty(file) || file.Any(c => c == '/' || c == '\\'))
            return BadRequest();

        EnsureDirectory();

        string path = Path.Combine(Directory, file);
        if (System.IO.File.Exists(path) && !overwrite)
            return StatusCode(409);

        Logger.Put($"IP {ip} is uploading file {file}", LogType.Debug);

        using FileStream fs = System.IO.File.Create(path);
        
        await fileStream.CopyToAsync(fs);

        Logger.Put($"IP {ip} uploaded {file} that is {fs.Length / 1024} KB long", LogType.Debug);

        string url = Request.GetDisplayUrl().Split('?')[0];
        url = portRegex.Replace(url, "");

        return Created(url.Replace("upload", "dl"), null);
    }

    [GeneratedRegex("\\:\\d{1,5}")]
    private static partial Regex PortRegex();
}
