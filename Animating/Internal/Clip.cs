using Unity.Entities;

namespace Graphix
{
    public enum ChannelPath
    {
        TRANSLATION,
        ROTATION,
        SCALE,
        WEIGHTS
    };

    public struct Channel
    {
        public ChannelPath Path;
        public Sampler Sampler;
    }

    public struct Clip
    {
        public BlobArray<Channel> Channels;
        public float Duration;
    }
}
