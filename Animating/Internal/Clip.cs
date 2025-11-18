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

        public unsafe void Vec3(float3* output, float3* channel_output, float time)
        {
            int index = Seek(time);
            if (index >= 0)
            {
                *output = *(channel_output + index);
            }
            else
            {
                int next = ~index;
                int prev = next - 1;

                float t = (time - Input[prev]) / (Input[next] - Input[prev]);
                *output = math.lerp(*(channel_output + prev), *(channel_output + next), t);
            }
        }

        public unsafe void Quat(quaternion* output, quaternion* channel_output, float time)
        {
            int index = Seek(time);
            if (index >= 0)
            {
                *output = *(channel_output + index);
            }
            else
            {
                int next = ~index;
                int prev = next - 1;

                float t = (time - Input[prev]) / (Input[next] - Input[prev]);
                *output = math.slerp(*(channel_output + prev), *(channel_output + next), t);
            }
        }

        public unsafe int Sample(float* output, float time)
        {
            switch (Path)
            {
                case ChannelPath.TRANSLATION:
                case ChannelPath.SCALE:
                    Vec3((float3*)output, (float3*)this.Output.GetUnsafePtr(), time);
                    return 3;
                case ChannelPath.ROTATION:
                    Quat((quaternion*)output, (quaternion*)this.Output.GetUnsafePtr(), time);
                    return 4;

                default:
                    throw new Exception($"unsupported channel path: {Path}");
            }
        }
    }

    public struct Clip
    {
        public BlobArray<Channel> Channels;

        public float Duration;

        public int Outputs;

        public unsafe void Sample(float* output, float time)
        {
            for (int i = 0; i < Channels.Length; i++)
            {
                output += Channels[i].Sample(output, time);
            }
        }
    }
}