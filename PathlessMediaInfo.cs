using Xabe.FFmpeg;

namespace SuperCoolWebServer;

internal class PathlessMediaInfo : IMediaInfo
{
    internal class PathlessStream : IStream
    {
        internal PathlessStream(IStream originalStream) => this.originalStream = originalStream;
        readonly IStream originalStream;

        public string Path => "";

        public int Index => originalStream.Index;

        public string Codec => originalStream.Codec;

        public StreamType StreamType => originalStream.StreamType;

        public string BuildParameters(ParameterPosition forPosition)
        {
            return originalStream.BuildParameters(forPosition);
        }

        public IEnumerable<string> GetSource()
        {
            return originalStream.GetSource();
        }
    }

    internal PathlessMediaInfo(IMediaInfo originalInfo) => this.originalInfo = originalInfo;
    readonly IMediaInfo originalInfo;

    public IEnumerable<IStream> Streams => originalInfo.Streams.Select(s => new PathlessStream(s));

    public string Path => "";

    public TimeSpan Duration => originalInfo.Duration;

    public DateTime? CreationTime => originalInfo.CreationTime;

    public long Size => originalInfo.Size;

    public IEnumerable<IVideoStream> VideoStreams => originalInfo.VideoStreams;

    public IEnumerable<IAudioStream> AudioStreams => originalInfo.AudioStreams;

    public IEnumerable<ISubtitleStream> SubtitleStreams => originalInfo.SubtitleStreams;
}
