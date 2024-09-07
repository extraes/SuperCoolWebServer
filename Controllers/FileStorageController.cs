using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;

namespace SuperCoolWebServer.Controllers;

[Route("api/files/[action]/{file}")]
public class FileStorageController : Controller
{
    const int MB_SIZE = 1024 * 1024;
    static string Directory => Path.GetFullPath(Config.values.filestoreDir);
    static ConditionalWeakTable<string, byte[]> cachedFiles = new();

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
    public async Task<IActionResult> Download(string file)
    {
        if (!Request.Headers.TryGetValue("cf-connecting-ip", out var ip))
            ip = Request.HttpContext.Connection.RemoteIpAddress?.ToString();

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

        if (finf.Length < 20 * MB_SIZE) // dont cache files larger than 20mb
        {
            if (!cachedFiles.TryGetValue(file, out byte[]? bytes))
            {
                bytes = await System.IO.File.ReadAllBytesAsync(finf.FullName);
                cachedFiles.Add(file, bytes);
            }

            return File(bytes, mime/*, finf.Name*/);
        }
        
        return PhysicalFile(finf.FullName, mime, true);
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

        return Created(url.Replace("upload", "dl"), null);
    }
}
