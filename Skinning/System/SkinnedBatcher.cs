using Bastard;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;

namespace Graphix
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(BatchGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SkinnedBatcher : ISystem
    {
        private static readonly int s_JOINTS = Shader.PropertyToID("_JointMap");

        private BatcherImpl m_Batcher;

        private Profile.Handle m_BatchHandle;

        public void OnCreate(ref SystemState state)
        {
            m_Batcher = new(Allocator.Persistent);
        }

        unsafe public void OnUpdate(ref SystemState state)
        {
            if (m_BatchHandle.Entry == 0)
            {
                m_BatchHandle = Profile.DefineEntry("SkinBatcher");
            }

            using (m_BatchHandle.Auto())
            {
                var MaterialMeshInfoBuffered = SystemAPI.GetBufferTypeHandle<MaterialMeshInfoBuffered>(true);
                var LocalToWorld = SystemAPI.GetComponentTypeHandle<LocalToWorld>(true);
                var SkinInfo = SystemAPI.GetComponentTypeHandle<SkinInfo>(true);
                var MaterialMeshArray = SystemAPI.ManagedAPI.GetSharedComponentTypeHandle<MaterialMeshArray>();

                state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

                var skinArray = SkinArray.GetCurrent(state.EntityManager);

                using var scope = m_Batcher.MakeScope();

                foreach (var chunk in SystemAPI.QueryBuilder().WithAll<MaterialMeshInfoBuffered, SkinInfo, SkinArray>().Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    var materialMeshArray = chunk.GetSharedComponentIndex(MaterialMeshArray);
                    ref var queue = ref EntitiesGraphicsSystemUnmanaged.GetQueue(materialMeshArray);
                    var chunkScope = scope.MakeChunk(ref queue);

                    var materialMeshAccessor = chunk.GetBufferAccessor(ref MaterialMeshInfoBuffered);

                    var SkinInfos = chunk.GetNativeArray(ref SkinInfo);
                    for (int entity = 0; entity < chunk.Count; entity++)
                    {
                        var mmb = materialMeshAccessor[entity];
                        var mmp = (MaterialMeshInfo*)mmb.GetUnsafeReadOnlyPtr();
                        var skin = SkinInfos[entity];
                        var texture = skinArray.GetCurrentStore(skin).Texture;
                        for (int i = 0; i < mmb.Length; i++)
                        {
                            var length = queue.Length;
                            var batchIndex = chunkScope.Record(materialMeshArray, mmp[i], entity, i, skin.Skin);
                            if (queue.Length != length)
                            {
                                queue.ElementAt(batchIndex).PropertyTextureBind(s_JOINTS, texture);
                            }
                        }
                    }

                    chunkScope.Flush(ref state, in chunk, ref LocalToWorld);
                }
            }
        }
    }
}
