using System;
using Bastard;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Graphix
{
    [UpdateInGroup(typeof(AnimationSamplerGroup))]
    partial struct Solo : ISystem
    {
        private int m_ProfileEntry;

        public void OnCreate(ref SystemState state)
        {
            m_ProfileEntry = Profile.DefineEntry("Solo");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using (new Profile.Scope(m_ProfileEntry))
            {
                foreach (var (animation, clipBingings, channelTargets, entity) in SystemAPI.Query<RefRO<AnimationState>, DynamicBuffer<ClipBinging>, DynamicBuffer<ChannelTarget>>().WithEntityAccess())
                {
                    ref var clipBinging = ref clipBingings.ElementAt(animation.ValueRO.ClipIndex);
                    var result = new NativeArray<float>(clipBinging.Outputs, Allocator.Temp);
                    unsafe
                    {
                        clipBinging.Blob.Value.Sample((float*)result.GetUnsafePtr(), animation.ValueRO.Time);
                    }

                    ref var channels = ref clipBinging.Blob.Value.Channels;
                    var offset = 0;
                    for (int i = 0; i < channels.Length; i++)
                    {
                        ref var channel = ref channels[i];
                        var target = channelTargets.ElementAt(clipBinging.TargetIndex + i).Value;
                        switch (channel.Path)
                        {
                            case ChannelPath.TRANSLATION:
                                if (target != Entity.Null)
                                {
                                    // UnityGLTF ToUnityVector3Convert
                                    SystemAPI.GetComponentRW<LocalTransform>(target).ValueRW.Position = new float3(-result[offset], result[offset + 1], result[offset + 2]);
                                }
                                offset += 3;
                                break;
                            case ChannelPath.ROTATION:
                                if (target != Entity.Null)
                                {
                                    // UnityGLTF ToUnityQuaternionConvert
                                    SystemAPI.GetComponentRW<LocalTransform>(target).ValueRW.Rotation = new float4(result[offset], -result[offset + 1], -result[offset + 2], result[offset + 3]);
                                }
                                offset += 4;
                                break;
                            case ChannelPath.SCALE:
                                if (target != Entity.Null)
                                {
                                    SystemAPI.GetComponentRW<LocalTransform>(target).ValueRW.Scale = result[offset];
                                }
                                offset += 3;
                                break;
                            default:
                                throw new Exception($"unsupported path: ${channel.Path}");
                        }
                    }
                }
            }
        }
    }
}