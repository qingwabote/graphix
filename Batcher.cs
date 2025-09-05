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
                foreach (var (mm, world, _) in SystemAPI.Query<MaterialMeshInfo, RefRO<LocalToWorld>, RefRW<BatchOutput>>().WithOptions(EntityQueryOptions.FilterWriteGroup))
                {
                    if (Batch.Register(HashCode.Combine(mm.Mesh, mm.Material), out Batch batch))
                    {
                        batch.Material = mm.Material;
                        batch.Mesh = mm.Mesh;
                    }
                    batch.InstanceWorlds.Add(world.ValueRO.Value);
                    batch.InstanceCount++;
                }
            }
        }
    }
}