using System.Text.Json.Serialization;

namespace SuperCoolWebServer.Cobalt;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CobaltFilenamePattern
{
    classic,
    pretty,
    basic,
    nerdy,
}
