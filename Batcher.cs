using Bastard;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace Graphix
{
    public partial struct Batcher : ISystem
    {
        static private BatcherImpl<MaterialMeshInfo, BatchSorter> s_Batcher = new();

        private int m_BatchEntry;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MaterialMeshInfo>();
        }

        unsafe public void OnUpdate(ref SystemState state)
        {
            if (m_BatchEntry == 0)
            {
                m_BatchEntry = Profile.DefineEntry("Batch");
            }

            using (new Profile.Scope(m_BatchEntry))
            {
                var MaterialMesh = SystemAPI.GetComponentTypeHandle<MaterialMeshInfo>(true);
                MaterialMesh.Update(ref state);
                var LocalToWorld = SystemAPI.GetComponentTypeHandle<LocalToWorld>(true);
                LocalToWorld.Update(ref state);

                state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

                foreach (var chunk in SystemAPI.QueryBuilder().WithAll<MaterialMeshInfo>().Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    s_Batcher.BeginChunk(ref state, chunk);
                    var mms = chunk.GetNativeArray(ref MaterialMesh);
                    var worlds = chunk.GetNativeArray(ref LocalToWorld);
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        s_Batcher.Add(mms[i], worlds.ElementAtRO(i), i);
                    }
                    s_Batcher.EndChunk();
                }
                s_Batcher.Clear();
            }
        }
    }
}