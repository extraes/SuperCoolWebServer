using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using System.Reflection.Emit;
using Xabe.FFmpeg;

namespace SuperCoolWebServer.Controllers;

[Route("api/ffmpeg/[action]")]
public class FfmpegController : Controller
{
    const int MB_SIZE = 1024 * 1024;

    [HttpPut]
    [ActionName("probe")]
    [RequestSizeLimit(50 * MB_SIZE)]
    [Consumes("video/mp4", "video/webm")]
    public async Task<IActionResult> Probe([FromBody] Stream videoFile)
    {
        if (!Request.Headers.TryGetValue("cf-connecting-ip", out var ip))
            ip = Request.HttpContext.Connection.RemoteIpAddress?.ToString();

        if (videoFile is null)
            return BadRequest();

        string filePath = Path.GetTempFileName();
        using (FileStream fs = System.IO.File.OpenWrite(filePath))
        {
            await videoFile.CopyToAsync(fs);
        }

        IMediaInfo info;
        try
        {
            info = await FFmpeg.GetMediaInfo(filePath);
        }
        catch(Exception ex)
        {
            Logger.Warn($"Exception while probing file from {ip} - {ex}");
            return UnprocessableEntity(ex);
        }

        try
        {

        }
        catch (Exception ex)
        {
            Logger.Warn($"Exception while deleting probed file from {ip} - {ex}");
        }

        return Ok(new PathlessMediaInfo(info));
    }
}
