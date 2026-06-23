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
        private Profile.Handle m_ProfileHandle;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SkinInfo>();
            state.RequireForUpdate<SkinArray>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (m_ProfileHandle.Entry == 0)
            {
                m_ProfileHandle = Profile.DefineEntry("JointAlloc");
            }

            using (m_ProfileHandle.Auto())
            {
                var SkinInfo = SystemAPI.GetComponentTypeHandle<SkinInfo>(true);
                var AnimationState = SystemAPI.GetComponentTypeHandle<AnimationState>(true);
                var ClipBinging = SystemAPI.GetBufferTypeHandle<ClipBinging>(true);
                var JointSource = SystemAPI.GetComponentTypeHandle<JointSource>(false);
                var JointOffset = SystemAPI.GetComponentTypeHandle<JointOffset>(false);
                var ChannelTarget = SystemAPI.GetBufferTypeHandle<ChannelTarget>(false);
                var SkinArray = SystemAPI.ManagedAPI.GetSharedComponentTypeHandle<SkinArray>();

                foreach (var chunk in SystemAPI.QueryBuilder().WithAll<SkinInfo, SkinArray>().Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    var skinArray = chunk.GetSharedComponentManaged(SkinArray, state.EntityManager);
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
                        BlobAssetReference<Clip> clip = default;
                        int frame = -1;
                        if (info.Baking)
                        {
                            if (animated)
                            {
                                var anim = animations[i];
                                clip = clips[i][anim.Index].Blob;
                                var duration = clip.Value.Duration;
                                var ratio = anim.Time / duration;
                                frame = (int)math.ceil(ratio * (duration * 60 - 1));
                            }
                            baked = skinArray.GetOffset(info, clip, frame, out offset);
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
                                skinArray.SetOffset(info, clip, frame, offset);
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
        private Profile.Handle m_ProfileHandle;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<JointSource>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (m_ProfileHandle.Entry == 0)
            {
                m_ProfileHandle = Profile.DefineEntry("JointUpdate");
            }

            using (m_ProfileHandle.Auto())
            {
                var models = new NativeList<float4x4>(Allocator.Temp);
                foreach (var (skin, nodes, source, offset) in SystemAPI.Query<SkinInfo, DynamicBuffer<SkinNode>, RefRW<JointSource>, JointOffset>())
                {
                    unsafe
                    {
                        if (source.ValueRO.Value == null)
                        {
                            continue;
                        }
                    }
                    models.Resize(nodes.Length + 1, NativeArrayOptions.UninitializedMemory);
                    models[0] = float4x4.identity;
                    ref var inverseBindMatrices = ref skin.JointMeta.Value.InverseBindMatrices;
                    ref var locations = ref skin.JointMeta.Value.Locations;
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

}
