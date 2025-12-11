using Bastard;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace Graphix
{
    [UpdateInGroup(typeof(BatchGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct Batcher : ISystem
    {
        private int m_BatchEntry;

        public void OnUpdate(ref SystemState state)
        {
            if (m_BatchEntry == 0)
            {
                m_BatchEntry = Profile.DefineEntry("Batcher");
            }

            using (new Profile.Scope(m_BatchEntry))
            {
                var MaterialMesh = SystemAPI.GetComponentTypeHandle<MaterialMeshInfo>(true);
                var LocalToWorld = SystemAPI.GetComponentTypeHandle<LocalToWorld>(true);
                var MaterialMeshArray = SystemAPI.ManagedAPI.GetSharedComponentTypeHandle<MaterialMeshArray>();

                state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

                var batcher = new BatcherImpl<MaterialMeshInfo, BatchProgram>(MaterialMeshArray, 128);
                // make MaterialMeshInfo RW for WriteGroup
                foreach (var chunk in SystemAPI.QueryBuilder().WithAllRW<MaterialMeshInfo>().WithOptions(EntityQueryOptions.FilterWriteGroup).Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    batcher.BeginChunk(ref state, chunk);
                    var mms = chunk.GetNativeArray(ref MaterialMesh);
                    var worlds = chunk.GetNativeArray(ref LocalToWorld);
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        batcher.Add(i, worlds.ElementAtRO(i).Value, mms[i]);
                    }
                    batcher.EndChunk();
                }
            }
        }
    }
}