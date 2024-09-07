namespace SuperCoolWebServer;

internal static class DiscordFormat
{
    public const string LARGE_VIDEO_FORMAT = """
        <html lang="en">
            <head>
                <meta property="og:image" content="{0}">
                <meta property="og:type" content="video.other">
                <meta property="og:video:url" content="{1}">
                <meta property="og:video:secure_url" content="{1}">
                <meta property="og:video:width" content="{2}">
                <meta property="og:video:height" content="{3}">
            </head>
        </html>
        """;
}
