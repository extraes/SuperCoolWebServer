using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using Xabe.FFmpeg;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace SuperCoolWebServer.Controllers;

[Route("ytdlp/[action]")]
public class YtDlpController : Controller
{
    [ThreadStatic]
    private static HttpClient? client;
    [ThreadStatic]
    private static YoutubeDL ytdlClient;

    static HttpClient Client
    {
        get
        {
            if (client is null)
            {
                client = new HttpClient();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }
            return client;
        }
    }

    static YoutubeDL Ytdl
    {
        get
        {
            ytdlClient ??= new YoutubeDL
            {
                YoutubeDLPath = YtDlpPath,
                FFmpegPath = FFmpegPath,
            };
            return ytdlClient;
        }
    }

    // YoutubeDLSharp downloads into the CWD by default; resolve absolute paths so readiness checks and execution agree.
    static readonly string YtDlpPath = Path.Combine(Directory.GetCurrentDirectory(), Utils.YtDlpBinaryName);
    static readonly string FFmpegPath = Path.Combine(Directory.GetCurrentDirectory(), Utils.FfmpegBinaryName);
    static readonly SemaphoreSlim provisionLock = new(1, 1);
    static readonly TimeSpan provisionRetryCooldown = TimeSpan.FromMinutes(5);
    static DateTime lastProvisionFailure = DateTime.MinValue;

    /// <summary>
    /// Makes sure the yt-dlp binary exists and is executable, downloading it if needed.
    /// Safe to call concurrently; failed downloads are retried at most once per cooldown period.
    /// </summary>
    internal static async Task<bool> EnsureYtDlpAsync()
    {
        if (IsUsable(YtDlpPath))
            return true;

        await provisionLock.WaitAsync();
        try
        {
            if (IsUsable(YtDlpPath))
                return true;

            if (!System.IO.File.Exists(YtDlpPath))
            {
                if (DateTime.UtcNow - lastProvisionFailure < provisionRetryCooldown)
                    return false;

                try
                {
                    await Utils.DownloadYtDlp(Directory.GetCurrentDirectory());
                }
                catch (Exception ex)
                {
                    lastProvisionFailure = DateTime.UtcNow;
                    Logger.Warn("Failed to download yt-dlp!", ex);
                    return false;
                }
            }

            // YoutubeDLSharp doesn't set the execute bit on Unix.
            if (!OperatingSystem.IsWindows())
            {
                System.IO.File.SetUnixFileMode(YtDlpPath, System.IO.File.GetUnixFileMode(YtDlpPath)
                    | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
            }

            return IsUsable(YtDlpPath);
        }
        finally
        {
            provisionLock.Release();
        }
    }

    static bool IsUsable(string path)
    {
        if (!System.IO.File.Exists(path))
            return false;
        if (OperatingSystem.IsWindows())
            return true;
        return (System.IO.File.GetUnixFileMode(path) & UnixFileMode.UserExecute) != 0;
    }

    static Timer fileDeleteTimer;
    static List<string> files = new();

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    static YtDlpController()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    {
        fileDeleteTimer = new((_) => DeleteOldFiles(), null, TimeSpan.Zero, TimeSpan.FromMinutes(15));
    }

    static void DeleteOldFiles()
    {
        for (int i = 0; i < files.Count; i++)
        {
            string? file = files[i];
            try
            {
                System.IO.File.Delete(file);
                Logger.Put($"Successfully deleted file: {file}");
                files.Remove(file);
                i--;
            }
            catch
            {
                Logger.Put($"Failed to delete file, will try again later. File: {file}");
            }
        }
    }

    [HttpGet]
    [ActionName("get")]
    public async Task<IActionResult> DownloadLinkContents(string link, bool instagramFallback = true)
    {
        if (string.IsNullOrEmpty(link))
            return BadRequest();

        if (instagramFallback && link.Contains("instagram.com", StringComparison.InvariantCultureIgnoreCase))
            return await DownloadInstagramContents(link);

        Stopwatch sw = Stopwatch.StartNew();

        string outputtington = "";
        string dest = "";
        var destinationSetter = new Progress<string>(str =>
        {
            outputtington += str + "\n";

            if (str.Contains("Merging formats"))
                dest = str.Split("\"")[1].Trim();
        });
        
        OptionSet options = new()
        {
            Output = Path.GetTempFileName(), // gives me a full path in output & avoids unicode in filename
        };
        files.Add(options.Output); // to delete temp file lol
        if (System.IO.File.Exists(FFmpegPath))
            options.FfmpegLocation = FFmpegPath; // custom OptionSet bypasses YoutubeDL.FFmpegPath

        if (!await EnsureYtDlpAsync())
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "yt-dlp is unavailable right now, try again later.");

        var res = await Ytdl.RunWithOptions(link, options, HttpContext.RequestAborted, output: destinationSetter);
        if (HttpContext.RequestAborted.IsCancellationRequested)
            return NoContent();
        if (!res.Success)
            return StatusCode(500, string.Join("\n", res.ErrorOutput));
        if (string.IsNullOrEmpty(dest))
        {
            int id = Random.Shared.Next();
            Logger.Warn($"Error ID {id} - empty destimation! Full YT-DLP output below:\n\t{outputtington}");
            return NotFound($"Error: No destination found. Check logs for ID {id}");
        }

        Logger.Put($"Downloaded file to {dest}");

        
        //var fileInfo = await FFmpeg.GetMediaInfo(dest);
        //string mimeType = fileInfo.VideoStreams.First().Codec.ToLower() switch
        //{
        //    "h264" => "video/mp4",
        //    "vp9" => "video/webm",
        //    _ => "application/octet-stream"
        //};

        string ext = Path.GetExtension(dest);
        bool isVideo = ext switch
        {
            ".mp4" => true,
            ".webm" => true,
            ".mkv" => true,
            _ => false,
        };
        files.Add(dest);
        return PhysicalFile(dest, "application/octet-stream", isVideo ? $"downloaded_video{ext}" : $"downloaded_image{ext}", true);
    }

    private async Task<IActionResult> DownloadInstagramContents(string link)
    {

        int status = StatusCodes.Status500InternalServerError;
        try
        {
            // let ddinstagram handle the private api fuckshit for me lol
            HttpRequestMessage httpReq = new(HttpMethod.Get, link.Replace("www.instagram.com", "d.ddinstagram.com"));
            httpReq.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:130.0) Gecko/20100101 Firefox/130.0 discord");
            HttpResponseMessage res = await Client.SendAsync(httpReq); // follows redirect to instagram

            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode, "DDInstagram/Instagram returned a failure response - " + await res.Content.ReadAsStringAsync());

            string fileName = res.RequestMessage?.RequestUri is not null ? Path.GetFileName(res.RequestMessage.RequestUri.ToString()).Split('?')[0] : "instagramvideo.mp4";
            Logger.Put("Proxying download from DDInstagram. Stock link: " + link);
            Stream retStream = await res.Content.ReadAsStreamAsync();
            return File(retStream, "application/octet-stream", fileName);
        }
        catch (Exception ex)
        {
            if (Debugger.IsAttached)
                throw;
            return StatusCode(status, ex.Message);
        }
    }
}
