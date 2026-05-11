using Bastard;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace Graphix
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(BatchGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct Batcher : ISystem
    {
        private BatcherImpl m_Batcher;

        private Profile.Handle m_BatchHandle;

        public void OnCreate(ref SystemState state)
        {
            m_Batcher = new(Allocator.Persistent);
        }

        // [BurstCompile]
        public unsafe void OnUpdate(ref SystemState state)
        {
            if (m_BatchHandle.Entry == 0)
            {
                m_BatchHandle = Profile.DefineEntry("Batcher");
            }

            using (m_BatchHandle.Auto())
            {
                var MaterialMeshInfo = SystemAPI.GetComponentTypeHandle<MaterialMeshInfo>(true);
                var MaterialMeshInfoBuffered = SystemAPI.GetBufferTypeHandle<MaterialMeshInfoBuffered>(true);
                var LocalToWorld = SystemAPI.GetComponentTypeHandle<LocalToWorld>(true);
                var MaterialMeshArray = SystemAPI.ManagedAPI.GetSharedComponentTypeHandle<MaterialMeshArray>();

                state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

                using var scope = m_Batcher.MakeScope();

                // make MaterialMeshInfo writable for WriteGroup
                foreach (var chunk in SystemAPI.QueryBuilder().WithAllRW<MaterialMeshInfo>().WithOptions(EntityQueryOptions.FilterWriteGroup).Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    var materialMeshArray = chunk.GetSharedComponentIndex(MaterialMeshArray);
                    ref var queue = ref EntitiesGraphicsSystemUnmanaged.GetQueue(materialMeshArray);
                    var chunkScope = scope.MakeChunk(ref queue);

                    var mms = chunk.GetNativeArray(ref MaterialMeshInfo);
                    for (int entity = 0; entity < chunk.Count; entity++)
                    {
                        chunkScope.Record(materialMeshArray, mms[entity], entity);
                    }

                    chunkScope.Flush(ref state, in chunk, ref LocalToWorld);
                }

                // make MaterialMeshInfoBuffered writable for WriteGroup
                foreach (var chunk in SystemAPI.QueryBuilder().WithAllRW<MaterialMeshInfoBuffered>().WithOptions(EntityQueryOptions.FilterWriteGroup).Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    var materialMeshArray = chunk.GetSharedComponentIndex(MaterialMeshArray);
                    ref var queue = ref EntitiesGraphicsSystemUnmanaged.GetQueue(materialMeshArray);
                    var chunkScope = scope.MakeChunk(ref queue);

                    var materialMeshAccessor = chunk.GetBufferAccessor(ref MaterialMeshInfoBuffered);

                    for (int entity = 0; entity < chunk.Count; entity++)
                    {
                        var mmb = materialMeshAccessor[entity];
                        var mmp = (MaterialMeshInfo*)mmb.GetUnsafeReadOnlyPtr();
                        for (int element = 0; element < mmb.Length; element++)
                        {
                            chunkScope.Record(materialMeshArray, mmp[element], entity, element);
                        }
                    }

                    chunkScope.Flush(ref state, in chunk, ref LocalToWorld);
                }
            }
        }

    }
}
