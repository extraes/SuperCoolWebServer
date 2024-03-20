using System.Text.Json.Serialization;

namespace SuperCoolWebServer.Cobalt;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CobaltAudioCodec
{
    best,
    mp3,
    ogg,
    wav,
    opus,
}
