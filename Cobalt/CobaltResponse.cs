using System.Text.Json.Serialization;

namespace SuperCoolWebServer.Cobalt;

public class CobaltResponse
{
    /// <summary>
    /// error / redirect / stream / success / rate-limit / picker
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
    /// <summary>
    /// various text, mostly used for errors
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    /// <summary>
    /// direct link to a file or a link to cobalt's live render
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
    /// <summary>
    /// various / images
    /// </summary>
    [JsonPropertyName("pickerType")]
    public string? PickerType { get; set; }
    /// <summary>
    /// array of picker items
    /// </summary>
    [JsonPropertyName("picker")]
    public CobaltPicker[] Picker { get; set; }
    /// <summary>
    /// direct link to a file or a link to cobalt's live render
    /// </summary>
    [JsonPropertyName("audio")]
    public string? Audio;
}
