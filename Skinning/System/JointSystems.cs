using Bastard;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Graphix
{
    [RequireMatchingQueriesForUpdate]
    public partial struct JointAllocator : ISystem
    {
        private static readonly Profile.Handle s_Profile = Profile.DefineEntry("JointAlloc");

        public void OnUpdate(ref SystemState state)
        {
            using var scope = s_Profile.Auto();

            var SkinInfo = SystemAPI.GetComponentTypeHandle<SkinInfo>(true);
            var AnimationState = SystemAPI.GetComponentTypeHandle<AnimationState>(true);
            var ClipBinging = SystemAPI.GetBufferTypeHandle<ClipBinging>(true);
            var JointSource = SystemAPI.GetComponentTypeHandle<JointSource>(false);
            var JointOffset = SystemAPI.GetComponentTypeHandle<JointOffset>(false);
            var ChannelTarget = SystemAPI.GetBufferTypeHandle<ChannelTarget>(false);

            foreach (var chunk in SystemAPI.QueryBuilder().WithAll<SkinInfo, JointSource>().Build().ToArchetypeChunkArray(Allocator.Temp))
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
                    var pose = PoseCache.Get(info.JointMeta);

                    int offset = -1;
                    ulong clipHash = 0;
                    int frameIndex = 0;
                    int frameCount = 1;
                    if (info.Baking)
                    {
                        if (animated)
                        {
                            var anim = animations[i];
                            var clip = clips[i][anim.Index].Blob;
                            frameCount = (int)(clip.Value.Duration * 60);
                            frameIndex = math.min((int)(anim.Time * 60), frameCount - 1);
                            clipHash = clip.GetDataHash();
                        }

                        offset = pose.GetOffset(clipHash, frameIndex);
                    }

                    if (offset == -1)
                    {
                        var store = pose.GetStore(info.Baking);
                        offset = store.Add();
                        unsafe
                        {
                            sources[i] = new JointSource(store.Source);
                        }

                        if (info.Baking)
                        {
                            pose.SetOffset(clipHash, frameIndex, frameCount, offset);
                        }
                    }

                    offsets[i] = new JointOffset { Value = offset };

                    if (animated)
                    {
                        unsafe
                        {
                            channelTargets[i] = sources[i].Value != null;
                        }
                    }
                }
            }
        }
    }

    [RequireMatchingQueriesForUpdate]
    public partial struct JointUpdater : ISystem
    {
        private static readonly Profile.Handle s_ProfileHandle = Profile.DefineEntry("JointUpdate");

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using (s_ProfileHandle.Auto())
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
