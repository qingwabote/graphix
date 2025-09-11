using Bastard;
using Budget;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Graphix
{
    public partial struct Batcher : ISystem
    {
        private int m_BatchEntry;

        public void OnCreate(ref SystemState state)
        {
            m_BatchEntry = Profile.DefineEntry("Batch");
        }

        public void OnUpdate(ref SystemState state)
        {
            using (new Profile.Scope(m_BatchEntry))
            {
                var MaterialMeshInfo = SystemAPI.GetComponentTypeHandle<MaterialMeshInfo>();
                MaterialMeshInfo.Update(ref state);
                var LocalToWorld = SystemAPI.GetComponentTypeHandle<LocalToWorld>(true);
                LocalToWorld.Update(ref state);

                state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

                // make MaterialMeshInfo RefRW for WriteGroup
                foreach (var chunk in SystemAPI.QueryBuilder().WithAllRW<MaterialMeshInfo>().WithOptions(EntityQueryOptions.FilterWriteGroup).Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    var mms = chunk.GetNativeArray(ref MaterialMeshInfo);
                    var worlds = chunk.GetNativeArray(ref LocalToWorld);
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var mm = mms[i];
                        if (Batch.Get(HashCode.Combine(mm.Mesh, mm.Material), out Batch batch))
                        {
                            batch.Material = mm.Material;
                            batch.Mesh = mm.Mesh;
                        }
                        batch.InstanceWorlds.Add(worlds.ElementAtRO(i).Value);
                        batch.InstanceCount++;
                    }
                }
            }
        }
    }
}