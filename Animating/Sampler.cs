using Unity.Entities;
using Unity.Mathematics;

namespace Graphix
{
    public struct Sampler
    {
        public BlobArray<float> Times;
        public BlobArray<float> Values;

        public readonly static float EPSILON = 1e-6f;

        public int Seek(float value)
        {
            if (value < Times[0])
            {
                return 0;
            }

            if (value > Times[Times.Length - 1])
            {
                return Times.Length - 1;
            }

            int head = 0;
            int tail = Times.Length - 1;
            while (head <= tail)
            {
                int mid = (head + tail) >> 1;
                float res = Times[mid];
                if ((value + EPSILON) < res)
                {
                    tail = mid - 1;
                }
                else if ((value - EPSILON) > res)
                {
                    head = mid + 1;
                }
                else
                {
                    return mid;
                }
            }
            return ~head;
        }

        public float Float(float time)
        {
            int index = Seek(time);
            if (index >= 0)
            {
                return Values[index];
            }

            int next = ~index;
            int prev = next - 1;

            float t = (time - Times[prev]) / (Times[next] - Times[prev]);
            return math.lerp(Values[prev], Values[next], t);
        }

        public unsafe float3 Vec3(float time)
        {
            float3* output = (float3*)Values.GetUnsafePtr();

            int index = Seek(time);
            if (index >= 0)
            {
                return *(output + index);
            }
            else
            {
                int next = ~index;
                int prev = next - 1;

                float t = (time - Times[prev]) / (Times[next] - Times[prev]);
                return math.lerp(*(output + prev), *(output + next), t);
            }
        }

        public unsafe quaternion Quat(float time)
        {
            quaternion* output = (quaternion*)Values.GetUnsafePtr();

            int index = Seek(time);
            if (index >= 0)
            {
                return *(output + index);
            }
            else
            {
                int next = ~index;
                int prev = next - 1;

                float t = (time - Times[prev]) / (Times[next] - Times[prev]);
                return math.slerp(*(output + prev), *(output + next), t);
            }
        }
    }
}
