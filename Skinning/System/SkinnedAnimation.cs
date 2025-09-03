using System;
using System.Collections.Generic;
using Bastard;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Graphix
{
    public partial struct SkinnedAnimationFilter : ISystem
    {
        private readonly struct ClipFrame : IEquatable<ClipFrame>
        {
            private readonly BlobAssetReference<Clip> m_Clip;
            private readonly int m_Frame;

            public ClipFrame(BlobAssetReference<Clip> clip, int frame)
            {
                m_Clip = clip;
                m_Frame = frame;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(m_Clip.GetHashCode(), m_Frame);
            }

            public bool Equals(ClipFrame other)
            {
                return m_Clip == other.m_Clip && m_Frame == other.m_Frame;
            }
        }

        private static readonly Dictionary<Skin, int> s_SkinOffsets = new();
        private static readonly NativeHashMap<ClipFrame, int> s_ClipFrameOffsets = new(1024, Allocator.Persistent);

        public void OnUpdate(ref SystemState state)
        {
            // foreach (var (infoComponent, joint) in SystemAPI.Query<SkinInfoComponent, RefRW<SkinJoint>>().WithNone<AnimationState>())
            // {
            //     var info = infoComponent.Value;
            //     if (info.Baking)
            //     {
            //     }
            //     else
            //     {
            //         var offset = info.Store.Add();
            //         // memory may be reallocated after Add();
            //         unsafe
            //         {
            //             joint.ValueRW.DataView = (long)(info.Store.Source + offset);
            //         }
            //         info.Offset = offset;
            //     }
            // }

            foreach (var (info, joint, anim, clips, entity) in SystemAPI.Query<SkinInfo, RefRW<SkinJoint>, RefRO<AnimationState>, DynamicBuffer<ClipBinging>>().WithEntityAccess())
            {
                int offset;
                if (info.Baking)
                {
                    var clip = clips.ElementAt(anim.ValueRO.ClipIndex);
                    var ratio = anim.ValueRO.Time / clip.Duration;
                    var frame = (int)math.ceil(ratio * (clip.Duration * 60 - 1));
                    var key = new ClipFrame(clip.Blob, frame);
                    if (s_ClipFrameOffsets.TryGetValue(key, out offset))
                    {
                        SystemAPI.SetBufferEnabled<ChannelTarget>(entity, false);
                    }
                    else
                    {
                        offset = info.Store.Add();
                        unsafe
                        {
                            joint.ValueRW.DataView.Value = new()
                            {
                                Data = (long)info.Store.Source,
                                Offset = offset
                            };
                        }
                        s_ClipFrameOffsets.Add(key, offset);

                        SystemAPI.SetBufferEnabled<ChannelTarget>(entity, true);
                    }
                }
                else
                {
                    offset = info.Store.Add();
                    unsafe
                    {
                        joint.ValueRW.DataView.Value = new()
                        {
                            Data = (long)info.Store.Source,
                            Offset = offset
                        };
                    }

                    SystemAPI.SetBufferEnabled<ChannelTarget>(entity, true);
                }
                info.Offset = offset;
            }
        }
    }

    public partial struct SkinnedAnimationUpdater : ISystem
    {
        private int m_ProfileEntry;

        public void OnCreate(ref SystemState state)
        {
            m_ProfileEntry = Profile.DefineEntry("SkinUpdate");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using (new Profile.Scope(m_ProfileEntry))
            {
                foreach (var (nodes, joint) in SystemAPI.Query<DynamicBuffer<SkinNode>, RefRO<SkinJoint>>())
                {
                    var DataView = joint.ValueRO.DataView.Value;
                    if (DataView.Data == 0)
                    {
                        continue;
                    }

                    var worlds = new NativeArray<float4x4>(nodes.Length, Allocator.Temp);
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        ref var node = ref nodes.ElementAt(i);
                        var local = SystemAPI.GetComponentRO<LocalTransform>(node.Target);
                        if (node.Parent == -1)
                        {
                            worlds[i] = float4x4.TRS(local.ValueRO.Position, local.ValueRO.Rotation, local.ValueRO.Scale);
                        }
                        else
                        {
                            worlds[i] = math.mul(worlds[node.Parent], float4x4.TRS(local.ValueRO.Position, local.ValueRO.Rotation, local.ValueRO.Scale));
                        }
                    }

                    ref var inverseBindMatrices = ref joint.ValueRO.InverseBindMatrices.Value.Data;
                    var jointOffset = joint.ValueRO.Index;
                    unsafe
                    {
                        var Data = (NativeArray<float>*)DataView.Data;
                        var matrices = (float4x3*)((float*)Data->GetUnsafePtr() + DataView.Offset);
                        for (int i = 0; i < inverseBindMatrices.Length; i++)
                        {
                            var m4x4 = math.mul(worlds[i + jointOffset], inverseBindMatrices[i]);
                            matrices[i].c0 = new float4(m4x4.c0.x, m4x4.c0.y, m4x4.c0.z, m4x4.c3.x);
                            matrices[i].c1 = new float4(m4x4.c1.x, m4x4.c1.y, m4x4.c1.z, m4x4.c3.y);
                            matrices[i].c2 = new float4(m4x4.c2.x, m4x4.c2.y, m4x4.c2.z, m4x4.c3.z);
                        }
                    }
                }
            }
        }
    }

    public partial struct SkinnedAnimationUploader : ISystem
    {
        private int m_ProfileEntry;

        public void OnCreate(ref SystemState state)
        {
            m_ProfileEntry = Profile.DefineEntry("SkinUpload");
        }

        public void OnUpdate(ref SystemState state)
        {
            using (new Profile.Scope(m_ProfileEntry))
            {
                foreach (var skin in SystemAPI.Query<SkinInfo>())
                {
                    skin.Store.Update();
                }
            }
        }
    }
}