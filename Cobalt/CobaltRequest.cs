using System.Text.Json.Serialization;

namespace SuperCoolWebServer.Cobalt;

public class CobaltRequest
{
    /// <summary>
    /// <b>must</b> be included in every request.
    /// </summary>
    [JsonPropertyName("url")]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public string Url { get; init; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    /// <summary>
    /// applies only to youtube downloads. h264 is recommended for phones.
    /// </summary>
    [JsonPropertyName("vCodec")]
    public CobaltVideoCodec? CobaltVideoCodec { get; set; }
    [JsonPropertyName("aCodec")]
    public CobaltAudioCodec? CobaltAudioCodec { get; set; }
    [JsonPropertyName("filenamePattern")]
    public CobaltFilenamePattern? CobaltFilenamePattern { get; set; }
    [JsonPropertyName("isAudioOnly")]
    public bool AudioOnly { get; set; }
    /// <summary>
    /// enables download of original sound used in a tiktok video.
    /// </summary>
    [JsonPropertyName("isTTFullAudio")]
    public bool TikTokFullAudio { get; set; }
    /// <summary>
    /// disables audio track in video downloads.
    /// </summary>
    [JsonPropertyName("isAudioMuted")]
    public bool MuteAudio { get; set; }
    /// <summary>
    /// backend uses Accept-Language header for youtube video audio tracks when true.
    /// </summary>
    [JsonPropertyName("dubLang")]
    public bool DubLang { get; set; }
    /// <summary>
    /// disables file metadata when set to true.
    /// </summary>
    [JsonPropertyName("disableMetadata")]
    public bool DisableMetadata { get; set; }
    /// <summary>
    /// changes whether twitter gifs are converted to .gif
    /// </summary>
    [JsonPropertyName("twitterGif")]
    public bool TwitterGif { get; set; }
}
