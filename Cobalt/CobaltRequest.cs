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
    /// Uses typical "XXXp" numbers. 144, 240, 360, 480, 720, 1080, 1440, 2160, 4320, etc. 720 recommended for phones.
    /// </summary>
    [JsonPropertyName("videoQuality")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? VideoQuality { get; set; }
    [JsonPropertyName("audioFormat")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CobaltAudioCodec? AudioCodec { get; set; }
    /// <summary>
    /// specifies the bitrate to use for the audio. applies only to audio conversion.
    /// </summary>
    [JsonPropertyName("audioBitrate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AudioBitrate { get; set; }
    [JsonPropertyName("filenameStyle")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CobaltFilenamePattern? CobaltFilenamePattern { get; set; }
    [JsonPropertyName("downloadMode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CobaltDownloadMode? DownloadMode { get; set; }
    /// <summary>
    /// applies only to youtube downloads. h264 is recommended for phones.
    /// </summary>
    [JsonPropertyName("youtubeVideoCodec")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CobaltVideoCodec? VideoCodec { get; set; }
    /// <summary>
    /// specifies the language of audio to download when a youtube video is dubbed.
    /// </summary>
    [JsonPropertyName("youtubeDubLang")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DubLang { get; set; }
    /// <summary>
    /// tunnels all downloads through the server, even when not necessary.
    /// </summary>
    [JsonPropertyName("alwaysProxy")]
    public bool AlwaysProxy { get; set; } = false;
    /// <summary>
    /// disables file metadata when set to true.
    /// </summary>
    [JsonPropertyName("disableMetadata")]
    public bool DisableMetadata { get; set; } = false;
    /// <summary>
    /// enables download of original sound used in a tiktok video.
    /// </summary>
    [JsonPropertyName("tiktokFullAudio")]
    public bool TikTokFullAudio { get; set; } = false;
    /// <summary>
    /// changes whether twitter gifs are converted to .gif
    /// </summary>
    [JsonPropertyName("twitterGif")]
    public bool TwitterGif { get; set; } = true;
    /// <summary>
    /// specifies whether to use HLS for downloading video or audio from youtube.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("youtubeHLS")]
    public bool? UseYouTubeHLS { get; set; }
}
