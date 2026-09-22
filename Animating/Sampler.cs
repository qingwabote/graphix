using Unity.Entities;
using Unity.Mathematics;

namespace Graphix
{
    public struct Sampler
    {
        public BlobArray<float> Times;
        public BlobArray<float> Values;

        public unsafe float Float(float time)
        {
            return Bastard.Sampler.Float((float*)Times.GetUnsafePtr(), (float*)Values.GetUnsafePtr(), Times.Length, time);
        }

        public unsafe float3 Vec3(float time)
        {
            return Bastard.Sampler.Vec3((float*)Times.GetUnsafePtr(), (float3*)Values.GetUnsafePtr(), Times.Length, time);
        }

        public unsafe quaternion Quat(float time)
        {
            return Bastard.Sampler.Quat((float*)Times.GetUnsafePtr(), (quaternion*)Values.GetUnsafePtr(), Times.Length, time);
        }
    }
}
