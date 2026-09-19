using Unity.Entities;

namespace Graphix
{
    public static class BlobAssetExtensions
    {
        public static unsafe ulong GetDataHash<T>(this BlobAssetReference<T> blob) where T : unmanaged
            => blob.m_data.Header->Hash;
    }
}