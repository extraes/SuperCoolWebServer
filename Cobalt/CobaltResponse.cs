using System.Text.Json.Serialization;

namespace SuperCoolWebServer.Cobalt;

public abstract class CobaltResponse
{
    /// <summary>
    /// Used only to access the status of the response. Re-deserialize to another class to access other properties.
    /// </summary>
    internal class Intermediate : CobaltResponse { }

    /// <summary>
    /// error / redirect / stream / success / rate-limit / picker
    /// </summary>
    [JsonPropertyName("status")]
    public CobaltResponseStatus Status { get; set; }
}
