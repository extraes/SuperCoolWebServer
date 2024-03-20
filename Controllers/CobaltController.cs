using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using SuperCoolWebServer.Cobalt;
using System.Net.Http.Headers;

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
    public async Task<IActionResult> DownloadLinkContents(string link, bool mp4Gif = false, string? useCobaltApiLink = null)
    {
        if (string.IsNullOrEmpty(link))
            return BadRequest();

        useCobaltApiLink ??= Config.values.defualtCobaltApi;
        useCobaltApiLink = useCobaltApiLink.TrimEnd('/').Replace("https://", "").Replace("http://", "");

        int status = 400;
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

            CobaltResponse? cobaltRes = await res.Content.ReadFromJsonAsync<CobaltResponse>();

            if (!res.IsSuccessStatusCode)
            {
                return StatusCode(status, cobaltRes?.Text ?? "Cobalt API returned an empty response");
            }


            if (cobaltRes?.Url is null)
                return StatusCode(500, "Cobalt API returned an empty response");

            Stream retStream = await Client.GetStreamAsync(cobaltRes.Url);
            string filename = Path.GetFileName(cobaltRes.Url).Split('?')[0];
            if (filename == "stream")
                filename = "twittergif.gif";
            return File(retStream, "application/octet-stream", filename);
        }
        catch (Exception ex)
        {
            return StatusCode(status, ex.Message);
        }
    }
}
