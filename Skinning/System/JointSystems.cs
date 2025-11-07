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
                                var clip = clips[i][anim.ClipIndex];
                                var ratio = anim.Time / clip.Duration;
                                var frame = (int)math.ceil(ratio * (clip.Duration * 60 - 1));
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
                                sources[i] = new JointSource { Value = (long)store.Source, };
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
                    if (source.ValueRO.Value == 0)
                    {
                        continue;
                    }

                    var models = new NativeArray<float4x4>(nodes.Length + 1, Allocator.Temp);
                    models[0] = float4x4.identity;
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        var node = nodes[i];
                        models[i + 1] = math.mul(models[node.Parent + 1], SystemAPI.GetComponent<LocalTransform>(node.Target).ToMatrix());
                    }

                    ref var inverseBindMatrices = ref skin.JointMeta.Value.InverseBindMatrices;
                    ref var Locations = ref skin.JointMeta.Value.Locations;
                    unsafe
                    {
                        var store = (NativeArray<float>*)source.ValueRO.Value;
                        var matrices = (float4x3*)((float*)store->GetUnsafePtr() + (int)offset.Value);
                        for (int i = 0; i < inverseBindMatrices.Length; i++)
                        {
                            var m = math.mul(models[i + skin.Joint + 1], inverseBindMatrices[i]);

                            float4x3* m4x3 = matrices + Locations[i];
                            m4x3->c0 = new float4(m.c0.x, m.c0.y, m.c0.z, m.c3.x);
                            m4x3->c1 = new float4(m.c1.x, m.c1.y, m.c1.z, m.c3.y);
                            m4x3->c2 = new float4(m.c2.x, m.c2.y, m.c2.z, m.c3.z);
                        }
                    }

                    source.ValueRW.Value = 0;
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