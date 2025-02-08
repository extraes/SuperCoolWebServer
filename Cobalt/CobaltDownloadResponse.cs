using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SuperCoolWebServer.Cobalt;

public class CobaltDownloadResponse : CobaltResponse
{
    /// <summary>
    /// url for the cobalt tunnel, or redirect to an external link
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; }
    /// <summary>
    /// cobalt-generated filename for the file being downloaded
    /// </summary>
    [JsonPropertyName("filename")]
    public string Filename { get; set; }
}