using System.Text.Json.Serialization;

namespace SuperCoolWebServer.Cobalt;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CobaltResponseStatus
{
    error,
    redirect,
    picker,
    tunnel,
}