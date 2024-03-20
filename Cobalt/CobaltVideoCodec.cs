using System.Text.Json.Serialization;

namespace SuperCoolWebServer.Cobalt;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CobaltVideoCodec
{
    h264,
    av1,
    vp9,
}
