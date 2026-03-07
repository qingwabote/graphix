using System;
using Bastard;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace Graphix
{
    [UpdateInGroup(typeof(AnimationSamplerGroup))]
    partial struct Solo : ISystem
    {
        private Profile.Handle m_ProfileHandle;

        private ComponentLookup<LocalTransform> m_LocalTransformLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AnimationState>();

            m_LocalTransformLookup = state.GetComponentLookup<LocalTransform>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (m_ProfileHandle.Entry == 0)
            {
                m_ProfileHandle = Profile.DefineEntry("Solo");
            }

            using (m_ProfileHandle.MakeScope())
            {
                m_LocalTransformLookup.Update(ref state);

                foreach (var (animation, bingings, targets) in SystemAPI.Query<AnimationState, DynamicBuffer<ClipBinging>, DynamicBuffer<ChannelTarget>>())
                {
                    ref var binging = ref bingings.ElementAt(animation.Index);
                    ref var clip = ref binging.Blob.Value;
                    ref var channels = ref clip.Channels;
                    for (int i = 0; i < channels.Length; i++)
                    {
                        var target = targets[binging.TargetIndex + i].Value;
                        if (target == Entity.Null) continue;
                        ref var channel = ref channels[i];
                        switch (channel.Path)
                        {
                            case ChannelPath.TRANSLATION:
                                m_LocalTransformLookup.GetRefRW(target).ValueRW.Position = channel.Vec3(animation.Time);
                                break;
                            case ChannelPath.ROTATION:
                                m_LocalTransformLookup.GetRefRW(target).ValueRW.Rotation = channel.Quat(animation.Time);
                                break;
                            case ChannelPath.SCALE:
                                m_LocalTransformLookup.GetRefRW(target).ValueRW.Scale = channel.Vec3(animation.Time).x;
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