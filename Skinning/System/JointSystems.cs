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
    public partial struct JointAllocator : ISystem
    {
        private readonly struct ClipFrame : IEquatable<ClipFrame>
        {
            private readonly int m_skin;
            private readonly BlobAssetReference<Clip> m_Clip;
            private readonly int m_Frame;

            public ClipFrame(int skin, BlobAssetReference<Clip> clip, int frame)
            {
                m_skin = skin;
                m_Clip = clip;
                m_Frame = frame;
            }

            public override int GetHashCode()
            {
                return Bastard.HashCode.Combine(m_skin, m_Clip.GetHashCode(), m_Frame);
            }

            public bool Equals(ClipFrame other)
            {
                return m_skin == other.m_skin && m_Clip == other.m_Clip && m_Frame == other.m_Frame;
            }
        }

        private static readonly NativeHashMap<ClipFrame, int> s_ClipFrameOffsets = new(1024, Allocator.Persistent);

        private int m_ProfileEntry;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SkinInfo>();
            state.RequireForUpdate<SkinArray>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (m_ProfileEntry == 0)
            {
                m_ProfileEntry = Profile.DefineEntry("JointAllocator");
            }

            using (new Profile.Scope(m_ProfileEntry))
            {
                var SkinInfo = SystemAPI.GetComponentTypeHandle<SkinInfo>(true);
                SkinInfo.Update(ref state);
                var AnimationState = SystemAPI.GetComponentTypeHandle<AnimationState>(true);
                AnimationState.Update(ref state);
                var ClipBinging = SystemAPI.GetBufferTypeHandle<ClipBinging>(true);
                ClipBinging.Update(ref state);
                var JointSource = SystemAPI.GetComponentTypeHandle<JointSource>(false);
                JointSource.Update(ref state);
                var JointOffset = SystemAPI.GetComponentTypeHandle<JointOffset>(false);
                JointOffset.Update(ref state);
                var ChannelTarget = SystemAPI.GetBufferTypeHandle<ChannelTarget>(false);
                ChannelTarget.Update(ref state);

                var skinArray = SkinArray.GetInstance(ref state);

                foreach (var chunk in SystemAPI.QueryBuilder().WithAll<SkinInfo>().Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    NativeArray<SkinInfo> infos = chunk.GetNativeArray(ref SkinInfo);
                    NativeArray<JointSource> sources = chunk.GetNativeArray(ref JointSource);
                    NativeArray<JointOffset> offsets = chunk.GetNativeArray(ref JointOffset);
                    NativeArray<AnimationState> animations = default;
                    BufferAccessor<ClipBinging> clips = default;
                    EnabledMask channelTargets = default;
                    bool animated = chunk.Has(ref AnimationState);
                    if (animated)
                    {
                        animations = chunk.GetNativeArray(ref AnimationState);
                        clips = chunk.GetBufferAccessor(ref ClipBinging);
                        channelTargets = chunk.GetEnabledMask(ref ChannelTarget);
                    }
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var info = infos[i];

                        int offset = -1;
                        bool baked = false;
                        ClipFrame key = new(info.Skin, default, -1);
                        if (info.Baking)
                        {
                            if (animated)
                            {
                                var anim = animations[i];
                                var clip = clips[i][anim.Index];
                                var duration = clip.Blob.Value.Duration;
                                var ratio = anim.Time / duration;
                                var frame = (int)math.ceil(ratio * (duration * 60 - 1));
                                key = new ClipFrame(info.Skin, clip.Blob, frame);
                            }
                            baked = s_ClipFrameOffsets.TryGetValue(key, out offset);
                        }
                        if (!baked)
                        {
                            var store = skinArray.GetCurrentStore(info);
                            offset = store.Add();
                            unsafe
                            {
                                sources[i] = new JointSource(store.Source);
                            }

                            if (info.Baking)
                            {
                                s_ClipFrameOffsets.Add(key, offset);
                            }
                        }
                        offsets[i] = new JointOffset { Value = offset };
                        if (animated)
                        {
                            channelTargets[i] = !baked;
                        }
                    }
                }
            }
        }
    }

    public partial struct JointUpdater : ISystem
    {
        private int m_ProfileEntry;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<JointSource>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (m_ProfileEntry == 0)
            {
                m_ProfileEntry = Profile.DefineEntry("JointUpdater");
            }

            using (new Profile.Scope(m_ProfileEntry))
            {
                foreach (var (skin, nodes, source, offset) in SystemAPI.Query<SkinInfo, DynamicBuffer<SkinNode>, RefRW<JointSource>, JointOffset>())
                {
                    unsafe
                    {
                        if (source.ValueRO.Value == null)
                        {
                            continue;
                        }
                    }

                    ref var inverseBindMatrices = ref skin.JointMeta.Value.InverseBindMatrices;
                    ref var locations = ref skin.JointMeta.Value.Locations;
                    var models = new NativeArray<float4x4>(nodes.Length + 1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    models[0] = float4x4.identity;
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        var node = nodes[i];
                        var model = math.mul(models[node.Parent + 1], SystemAPI.GetComponent<LocalTransform>(node.Target).ToMatrix());

                        var location = locations[i];
                        if (location > -1)
                        {
                            var matrix = math.mul(model, inverseBindMatrices[location]);
                            unsafe
                            {
                                float4x3* matrices = (float4x3*)((float*)source.ValueRO.Value->GetUnsafePtr() + (int)offset.Value);
                                matrices[location] = new float4x3(
                                    new float4(matrix.c0.x, matrix.c0.y, matrix.c0.z, matrix.c3.x),
                                    new float4(matrix.c1.x, matrix.c1.y, matrix.c1.z, matrix.c3.y),
                                    new float4(matrix.c2.x, matrix.c2.y, matrix.c2.z, matrix.c3.z)
                                );
                            }
                        }

                        models[i + 1] = model;
                    }

                    unsafe
                    {
                        source.ValueRW.Value = null;
                    }
                }
            }
        }
    }

    public partial struct JointUploader : ISystem
    {
        private int m_ProfileEntry;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SkinInfo>();
            state.RequireForUpdate<SkinArray>();

        }

        public void OnUpdate(ref SystemState state)
        {
            if (m_ProfileEntry == 0)
            {
                m_ProfileEntry = Profile.DefineEntry("JointUploader");
            }

            using (new Profile.Scope(m_ProfileEntry))
            {
                var skinArray = SkinArray.GetInstance(ref state);
                foreach (var info in SystemAPI.Query<SkinInfo>())
                {
                    skinArray.GetCurrentStore(info).Update();
                }
            }
        }
    }
}