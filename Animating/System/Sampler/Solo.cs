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

        private int m_Write;

        private ComponentLookup<LocalTransform> m_LocalTransformLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AnimationState>();

            m_LocalTransformLookup = state.GetComponentLookup<LocalTransform>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (m_ProfileEntry == 0)
            {
                m_ProfileEntry = Profile.DefineEntry("Solo");
                m_Write = Profile.DefineEntry("SoloWrite");
            }

            using (new Profile.Scope(m_ProfileEntry))
            {
                m_LocalTransformLookup.Update(ref state);

                var output = new NativeList<float>(Allocator.Temp);
                foreach (var (animation, bingings, targets) in SystemAPI.Query<AnimationState, DynamicBuffer<ClipBinging>, DynamicBuffer<ChannelTarget>>())
                {
                    ref var binging = ref bingings.ElementAt(animation.Index);
                    ref var clip = ref binging.Blob.Value;
                    output.Resize(clip.Outputs, NativeArrayOptions.UninitializedMemory);
                    unsafe
                    {
                        clip.Sample(output.GetUnsafePtr(), animation.Time);
                    }

                    Profile.Begin(m_Write);
                    ref var channels = ref clip.Channels;
                    var offset = 0;
                    for (int i = 0; i < channels.Length; i++)
                    {
                        ref var channel = ref channels[i];
                        var target = targets.ElementAt(binging.TargetIndex + i).Value;
                        switch (channel.Path)
                        {
                            case ChannelPath.TRANSLATION:
                                if (target != Entity.Null)
                                {
                                    m_LocalTransformLookup.GetRefRW(target).ValueRW.Position = new float3(output[offset], output[offset + 1], output[offset + 2]);
                                }
                                offset += 3;
                                break;
                            case ChannelPath.ROTATION:
                                if (target != Entity.Null)
                                {
                                    m_LocalTransformLookup.GetRefRW(target).ValueRW.Rotation = new float4(output[offset], output[offset + 1], output[offset + 2], output[offset + 3]);
                                }
                                offset += 4;
                                break;
                            case ChannelPath.SCALE:
                                if (target != Entity.Null)
                                {
                                    m_LocalTransformLookup.GetRefRW(target).ValueRW.Scale = output[offset];
                                }
                                offset += 3;
                                break;
                            default:
                                throw new Exception($"unsupported path: ${channel.Path}");
                        }
                    }
                    Profile.End(m_Write);
                }
            }
        }
    }
}