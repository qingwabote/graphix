using Bastard;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace Graphix
{
    public partial struct Batcher : ISystem
    {
        static private BatcherImpl<MaterialMesh, BatchSorter> s_Batcher = new();

        private int m_BatchEntry;

        public void OnCreate(ref SystemState state)
        {
            m_BatchEntry = Profile.DefineEntry("Batch");
        }

        unsafe public void OnUpdate(ref SystemState state)
        {
            using (new Profile.Scope(m_BatchEntry))
            {
                var MaterialMesh = SystemAPI.GetComponentTypeHandle<MaterialMesh>(true);
                MaterialMesh.Update(ref state);
                var LocalToWorld = SystemAPI.GetComponentTypeHandle<LocalToWorld>(true);
                LocalToWorld.Update(ref state);

                state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

                foreach (var chunk in SystemAPI.QueryBuilder().WithAll<MaterialMesh>().Build().ToArchetypeChunkArray(Allocator.Temp))
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