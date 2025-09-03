using System;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;

namespace Graphix
{
    [StructLayout(LayoutKind.Sequential, Size = 12)]
    public unsafe struct Vec3 { }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public unsafe struct Quat { }

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

        public unsafe void Lerp(float* result, float* a, float* b, float t)
        {
            result[0] = a[0] + t * (b[0] - a[0]);
            result[1] = a[1] + t * (b[1] - a[1]);
            result[2] = a[2] + t * (b[2] - a[2]);
        }

        public unsafe void Slerp(float* result, float* a, float* b, float t)
        {
            // benchmarks:
            //    http://jsperf.com/quaternion-slerp-implementations

            float scale0 = 0;
            float scale1 = 0;
            float bx = b[0];
            float by = b[1];
            float bz = b[2];
            float bw = b[3];

            // calc cosine
            float cosom = a[0] * b[0] + a[1] * b[1] + a[2] * b[2] + a[3] * b[3];
            // adjust signs (if necessary)
            if (cosom < 0.0)
            {
                cosom = -cosom;
                bx = -bx;
                by = -by;
                bz = -bz;
                bw = -bw;
            }
            // calculate coefficients
            if ((1.0 - cosom) > 0.000001)
            {
                // standard case (slerp)
                float omega = math.acos(cosom);
                float sinom = math.sin(omega);
                scale0 = math.sin((1.0f - t) * omega) / sinom;
                scale1 = math.sin(t * omega) / sinom;
            }
            else
            {
                // "from" and "to" quaternions are very close
                //  ... so we can do a linear interpolation
                scale0 = 1.0f - t;
                scale1 = t;
            }
            // calculate final values
            result[0] = scale0 * a[0] + scale1 * bx;
            result[1] = scale0 * a[1] + scale1 * by;
            result[2] = scale0 * a[2] + scale1 * bz;
            result[3] = scale0 * a[3] + scale1 * bw;
        }

        public unsafe void Vec3(Vec3* output, Vec3* channel_output, float time)
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
                Lerp((float*)output, (float*)(channel_output + prev), (float*)(channel_output + next), t);
            }
        }

        public unsafe void Quat(Quat* output, Quat* channel_output, float time)
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
                Slerp((float*)output, (float*)(channel_output + prev), (float*)(channel_output + next), t);
            }
        }

        public unsafe int Sample(float* output, float time)
        {
            switch (Path)
            {
                case ChannelPath.TRANSLATION:
                case ChannelPath.SCALE:
                    Vec3((Vec3*)output, (Vec3*)this.Output.GetUnsafePtr(), time);
                    return 3;
                case ChannelPath.ROTATION:
                    Quat((Quat*)output, (Quat*)this.Output.GetUnsafePtr(), time);
                    return 4;

                default:
                    throw new Exception($"unsupported channel path: {Path}");
            }
        }
    }

    public struct Clip
    {
        public BlobArray<Channel> Channels;

        public unsafe void Sample(float* output, float time)
        {
            for (int i = 0; i < Channels.Length; i++)
            {
                output += Channels[i].Sample(output, time);
            }
        }
    }
}