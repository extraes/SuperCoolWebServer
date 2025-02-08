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

            HttpRequestMessage httpReq = new(HttpMethod.Post, $"https://{useCobaltApiLink}/");
            httpReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpReq.Content = JsonContent.Create(req, new MediaTypeHeaderValue("application/json"));
            
            HttpResponseMessage res = await Client.SendAsync(httpReq);
            status = (int)res.StatusCode;

            rawCobaltResponse = await res.Content.ReadAsStringAsync();

            CobaltResponse? intermediateCobaltRes = null;
            try
            {   
                intermediateCobaltRes = System.Text.Json.JsonSerializer.Deserialize<CobaltResponse.Intermediate>(rawCobaltResponse);
                if (intermediateCobaltRes is null)
                    throw new Exception("Failed to deserialize intermediate cobalt response");
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to deserialize intermediate cobalt response: " + ex.Message);
                return StatusCode(500, "Failed to initial-deserialize cobalt response: " + ex.Message + "\nFrom cobalt: " + rawCobaltResponse);
            }

            switch (intermediateCobaltRes.Status)
            {
                case CobaltResponseStatus.error:
                    CobaltErrorResponse? errorRes = await res.Content.ReadFromJsonAsync<CobaltErrorResponse>();
                    if (errorRes is null)
                        return StatusCode(500, "Failed to deserialize cobalt error response: " + rawCobaltResponse);
                    else
                        return StatusCode(res.IsSuccessStatusCode ? 500 : (int)res.StatusCode, errorRes);
                case CobaltResponseStatus.redirect:
                case CobaltResponseStatus.tunnel:
                    CobaltDownloadResponse? cobaltRes = await res.Content.ReadFromJsonAsync<CobaltDownloadResponse>();
                    if (cobaltRes is null)
                        return StatusCode(500, "Failed to deserialize cobalt download response: " + rawCobaltResponse);

                    Stream retStream = await Client.GetStreamAsync(cobaltRes.Url);
                    Logger.Put("Proxying download from " + cobaltRes.Url);
                    string filename = cobaltRes.Filename ?? Path.GetFileName(cobaltRes.Url).Split('?')[0];
                    bool isTwitter = link.Contains("twitter.com") || link.Contains("x.com");
                    if (filename == "stream")
                    {
                        if (isTwitter)
                            filename = "twittergif.gif";
                        else if (link.Contains("youtu")) // bc youtu.be is a thing
                            filename = "youtube.mp4";
                        else
                            filename = "video.mp4";
                    }
                    return File(retStream, "application/octet-stream", filename);
                case CobaltResponseStatus.picker:
                    return StatusCode(400, "Pickers not supported. Use cobalt.tools directly.");
                default:
                    return StatusCode(500, "Unknown cobalt response status: " + intermediateCobaltRes.Status);
            }
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
