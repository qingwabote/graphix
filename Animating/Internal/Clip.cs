using System;
using Unity.Entities;
using Unity.Mathematics;

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
        public BlobArray<float> Input;
        public BlobArray<float> Output;

        public readonly static float EPSILON = 1e-6f;

        public int Seek(float value)
        {
            if (value < Input[0])
            {
                return 0;
            }

            if (value > Input[Input.Length - 1])
            {
                return Input.Length - 1;
            }

            int head = 0;
            int tail = Input.Length - 1;
            while (head <= tail)
            {
                int mid = (head + tail) >> 1;
                float res = Input[mid];
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

        public unsafe float3 Vec3(float time)
        {
            float3* output = (float3*)Output.GetUnsafePtr();

            int index = Seek(time);
            if (index >= 0)
            {
                return *(output + index);
            }
            else
            {
                int next = ~index;
                int prev = next - 1;

                float t = (time - Input[prev]) / (Input[next] - Input[prev]);
                return math.lerp(*(output + prev), *(output + next), t);
            }
        }

        public unsafe quaternion Quat(float time)
        {
            quaternion* output = (quaternion*)Output.GetUnsafePtr();

            int index = Seek(time);
            if (index >= 0)
            {
                return *(output + index);
            }
            else
            {
                int next = ~index;
                int prev = next - 1;

                float t = (time - Input[prev]) / (Input[next] - Input[prev]);
                return math.slerp(*(output + prev), *(output + next), t);
            }
        }
    }

    public struct Clip
    {
        public BlobArray<Channel> Channels;
        public float Duration;
    }
}