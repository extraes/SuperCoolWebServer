using Microsoft.AspNetCore.Mvc;
using StableCube.Media.Gifsicle;
using System.Reflection;

namespace SuperCoolWebServer.Controllers;

[Route("api/gifsicle/[action]")]
public class GifsicleController : Controller
{
    static readonly string gifsiclePath = OperatingSystem.IsLinux() ? "./runtimes/linux-x64/native/gifsicle" : "./runtimes/win-x64/native/gifsicle.exe";
    static readonly string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
    GifsicleService gifsicle = new(Path.GetFullPath(gifsiclePath, baseDir));

    [HttpPost]
    [ActionName("optimizeBlocking")]
    [Consumes("image/gif")]
    public async Task<IActionResult> Optimize([FromBody] Stream gifFile, int level)
    {
        level = Math.Clamp(level, 35, 200);
        string filePath = Path.GetTempFileName();
        using (FileStream fs = System.IO.File.OpenWrite(filePath))
        {
            await gifFile.CopyToAsync(fs);
        }

        var options = new GifsicleOptions()
            .WithLossyness(level)
            .WithOptimize(2)
            .WithOutput(Path.GetTempFileName());
        GifsicleCommandResult res = await gifsicle.RunAsync(filePath, options);

        if (res.ExitCode != 0)
            return StatusCode(500);
        
        byte[] optimizedGifData = await System.IO.File.ReadAllBytesAsync(options.Output);
        
        // clean up before ret
        System.IO.File.Delete(options.Output);
        System.IO.File.Delete(filePath);
        
        Logger.Put($"Produced {Math.Round(optimizedGifData.Length / 1024 / 1024.0, 2)}MB gif in {Math.Round((res.ExitTime - res.StartTime).TotalMilliseconds / 1000.0, 2)} seconds");
        
        return File(optimizedGifData, "image/gif", "optimized.gif");
    }
}
