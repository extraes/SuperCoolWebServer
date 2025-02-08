using System.Text.Json.Serialization;

namespace SuperCoolWebServer.Cobalt;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CobaltDownloadMode
{
    auto,
    /// <summary>
    /// Downloads audio only.
    /// </summary>
    audio,
    /// <summary>
    /// Skips audio track in videos
    /// </summary>
    mute,
}
