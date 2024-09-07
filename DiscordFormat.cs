namespace SuperCoolWebServer;

internal static class DiscordFormat
{
    public const string LARGE_VIDEO_FORMAT = """
        <!DOCTYPE html>
        <html lang="en">
            <head>
                <meta property="og:image" content="{0}">
                <meta property="og:type" content="video.other">
                <meta property="og:video:url" content="{1}">
                <meta property="og:video:secure_url" content="{1}">
                <meta property="og:video:width" content="{2}">
                <meta property="og:video:height" content="{3}">
            </head>
            <body>
              <p style="font-size: 1.2rem; font-family: sans-serif;">Copy this url into discord to see the embed<br>(Original video may be large)</p>
              <pre style="background: #333; color: white; border-radius: 3px; overflow: auto;">
                <code></code>
              </pre>
              <script>document.querySelector("code").textContent = document.head.innerHTML</script>
            </body>
        </html>
        """;
}
