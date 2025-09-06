using Bastard;
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
                // make MaterialMeshInfo RefRW for WriteGroup
                foreach (var (mm, world) in SystemAPI.Query<RefRW<MaterialMeshInfo>, RefRO<LocalToWorld>>().WithOptions(EntityQueryOptions.FilterWriteGroup))
                {
                    if (Batch.Register(HashCode.Combine(mm.ValueRO.Mesh, mm.ValueRO.Material), out Batch batch))
                    {
                        batch.Material = mm.ValueRO.Material;
                        batch.Mesh = mm.ValueRO.Mesh;
                    }
                    batch.InstanceWorlds.Add(world.ValueRO.Value);
                    batch.InstanceCount++;
                }
            }
        }
    }
}