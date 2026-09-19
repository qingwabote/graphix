using Bastard;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;

namespace Graphix
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(BatchGroup))]
    [CreateAfter(typeof(RenderContextSystem))]
    [RequireMatchingQueriesForUpdate]
    public unsafe partial struct SkinnedBatchSystem : ISystem
    {
        private static readonly int s_JOINTS = Shader.PropertyToID("_JointMap");

        private static readonly Profile.Handle s_Profile = Profile.DefineEntry("SkinBatcher");

        private Batcher m_Batcher;

        public void OnCreate(ref SystemState state)
        {
            ref var context = ref state.World.Unmanaged.GetUnsafeSystemRef<RenderContextSystem>(state.World.GetExistingSystem<RenderContextSystem>());
            m_Batcher = new((RenderContextSystem*)UnsafeUtility.AddressOf(ref context), Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            m_Batcher.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            using (s_Profile.Auto())
            {
                var MaterialMeshInfoBuffered = SystemAPI.GetBufferTypeHandle<MaterialMeshInfoBuffered>(true);
                var SkinInfo = SystemAPI.GetComponentTypeHandle<SkinInfo>(true);

                state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

                using var scope = m_Batcher.Auto();

                foreach (var chunk in SystemAPI.QueryBuilder().WithAll<MaterialMeshInfoBuffered, SkinInfo>().Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    using var batcher = scope.AutoChunk(in chunk);
                    var queue = batcher.Queue;

                    var materialMeshAccessor = chunk.GetBufferAccessor(ref MaterialMeshInfoBuffered);

                    var SkinInfos = chunk.GetNativeArray(ref SkinInfo);
                    for (int entity = 0; entity < chunk.Count; entity++)
                    {
                        var mmb = materialMeshAccessor[entity];
                        var mmp = (MaterialMeshInfo*)mmb.GetUnsafeReadOnlyPtr();
                        var skin = SkinInfos[entity];
                        var jointMetaHash = skin.JointMeta.GetDataHash();
                        for (int i = 0; i < mmb.Length; i++)
                        {
                            var length = queue->Length;
                            var batchIndex = batcher.Add(mmp[i], entity, i, (int)jointMetaHash);
                            if (queue->Length != length)
                            {
                                var store = PoseCache.Get(skin.JointMeta).GetStore(skin.Baking);
                                store.Update();
                                queue->ElementAt(batchIndex).PropertyTextureBind(s_JOINTS, store.Texture);
                            }
                        }
                    }
                }
            }
        }
    }
}
