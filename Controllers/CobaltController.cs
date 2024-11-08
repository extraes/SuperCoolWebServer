using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SuperCoolWebServer.Cobalt;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Web;

namespace SuperCoolWebServer.Controllers;

[Route("cobalt/[action]")]
public class CobaltController : Controller
{
    [ThreadStatic]
    private static HttpClient? client;

    static HttpClient Client
    {
        get
        {
            client ??= new HttpClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }
    }

    [HttpGet]
    [ActionName("get")]
    public async Task<IActionResult> DownloadLinkContents(string link, bool mp4Gif = false, string? useCobaltApiLink = null, bool instagramFallback = true)
    {
        if (string.IsNullOrEmpty(link))
            return BadRequest();

        if (instagramFallback && link.Contains("instagram.com", StringComparison.InvariantCultureIgnoreCase))
            return await DownloadInstagramContents(link);
            

        useCobaltApiLink ??= Config.values.defualtCobaltApi;
        useCobaltApiLink = useCobaltApiLink.TrimEnd('/').Replace("https://", "").Replace("http://", "");

        string? rawCobaltResponse = null;
        int status = StatusCodes.Status500InternalServerError;
        try
        {
            CobaltRequest req = new()
            {
                Url = link,
                TwitterGif = !mp4Gif,
            };

            HttpRequestMessage httpReq = new(HttpMethod.Post, $"https://{useCobaltApiLink}/api/json");
            httpReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpReq.Content = JsonContent.Create(req, new MediaTypeHeaderValue("application/json"));
            
            HttpResponseMessage res = await Client.SendAsync(httpReq);
            status = (int)res.StatusCode;

            rawCobaltResponse = await res.Content.ReadAsStringAsync();

            CobaltResponse? cobaltRes = await res.Content.ReadFromJsonAsync<CobaltResponse>();

            if (!res.IsSuccessStatusCode)
            {
                return StatusCode(status, cobaltRes?.Text ?? "Cobalt API returned an empty response");
            }


            if (cobaltRes?.Url is null)
                return StatusCode(500, "Cobalt API returned an empty response");

            Stream retStream = await Client.GetStreamAsync(cobaltRes.Url);
            Logger.Put("Proxying download from " + cobaltRes.Url);
            string filename = Path.GetFileName(cobaltRes.Url).Split('?')[0];
            bool isTwitter = link.Contains("twitter.com") || link.Contains("x.com");
            if (filename == "stream")
            {
                if (isTwitter)
                    filename = "twittergif.gif";
                else if (link.Contains("youtu"))
                    filename = "youtube.mp4";
                else
                    filename = "video.mp4";
            }
            return File(retStream, "application/octet-stream", filename);
        }
        catch (Exception ex)
        {
            if (rawCobaltResponse is not null)
                Logger.Warn($"Exception after getting response from cobalt! It may be an error response, see below:\n\t{rawCobaltResponse}");
            return StatusCode(status, ex.Message);
        }
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
