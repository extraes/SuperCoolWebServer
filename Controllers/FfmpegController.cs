using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using System.Reflection.Emit;
using Xabe.FFmpeg;

namespace SuperCoolWebServer.Controllers;

[Route("api/ffmpeg/[action]")]
public class FfmpegController : Controller
{
    [HttpPut]
    [ActionName("probe")]
    [Consumes("video/mp4", "video/webm")]
    public async Task<IActionResult> Probe([FromBody] Stream videoFile)
    {
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
            return UnprocessableEntity(ex);
        }

        return Ok(new PathlessMediaInfo(info));
    }

}
