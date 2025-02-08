using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SuperCoolWebServer.Cobalt;

public class CobaltErrorResponse : CobaltResponse
{
    public class CobaltError
    {
        public class ErrorContext
        {
            /// <summary>
            /// <b>Optional</b>, stating which service was being downloaded from. Defaults to an empty string.
            /// </summary>
            [JsonPropertyName("service")]
            public string Service { get; set; } = "";
            /// <summary>
            /// <b>Optional</b> number providing the ratelimit maximum number of requests, or maximum downloadable video duration. Defaults to -1.
            /// </summary>
            [JsonPropertyName("limit")]
            public double Limit { get; set; } = -1;
        }

        /// <summary>
        /// machine-readable error code explaining the failure reason
        /// </summary>
        [JsonPropertyName("code")]
        public string Code { get; set; } = "";
        /// <summary>
        /// <b>Optional</b> container for providing more context
        /// </summary>
        [JsonPropertyName("context")]
        public ErrorContext? Context { get; set; }
    }

    /// <summary>
    /// contains more context about the error
    /// </summary>
    [JsonPropertyName("error")]
    public CobaltError Error { get; set; }
}