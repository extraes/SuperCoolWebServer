using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SuperCoolWebServer.Cobalt;

public class CobaltPickerResponse : CobaltResponse
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PickerMediaType
    {
        photo,
        video,
        gif
    }

    public class PickerMedia
    {
        [JsonPropertyName("type")]
        public PickerMediaType Type { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; } = "";
        /// <summary>
        /// <b>Optional</b> thumbnail URL
        /// </summary>
        [JsonPropertyName("thumb")]
        public string? Thumbnail { get; set; }
    }

    /// <summary>
    /// <b>Optional</b>, returned when an image slideshow (such as on tiktok) has background audio.
    /// </summary>
    [JsonPropertyName("audio")]
    public string Audio { get; set; } = "";
    /// <summary>
    /// <b>Optional</b>, cobalt-generated filename, returned if <see cref="Audio"/> exists
    /// </summary>
    [JsonPropertyName("audioFilename")]
    public string AudioFilaname { get; set; } = "";
    /// <summary>
    /// array of objects containing the individual media
    /// </summary>
    [JsonPropertyName("picker")]
    public PickerMedia[] Picker { get; set; } = Array.Empty<PickerMedia>();
}