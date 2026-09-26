using Bag;
using Bastard;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace Graphix
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(BatchGroup))]
    [CreateAfter(typeof(EntitiesGraphicsSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct BatchSystem : ISystem
    {
        private static readonly Profile.Handle s_Profile = Profile.DefineEntry("Batcher");

        // escape from managed EntitiesGraphicsSystem
        private BatchQueue m_Queue;

        public void OnCreate(ref SystemState state)
        {
            m_Queue = state.World.GetExistingSystemManaged<EntitiesGraphicsSystem>().Queue;
        }

        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using (s_Profile.Auto())
            {
                var MaterialMeshInfo = SystemAPI.GetComponentTypeHandle<MaterialMeshInfo>(true);
                var MaterialMeshInfoBuffered = SystemAPI.GetBufferTypeHandle<MaterialMeshInfoBuffered>(true);

                state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

                // make MaterialMeshInfo writable for WriteGroup
                foreach (var chunk in SystemAPI.QueryBuilder().WithAllRW<MaterialMeshInfo>().WithOptions(EntityQueryOptions.FilterWriteGroup).Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    using var batcher = m_Queue.Auto(in chunk);

                    var mms = chunk.GetNativeArray(ref MaterialMeshInfo);
                    for (int entity = 0; entity < chunk.Count; entity++)
                    {
                        batcher.Add(mms[entity].Material, mms[entity].Mesh, entity);
                    }
                }

                // make MaterialMeshInfoBuffered writable for WriteGroup
                foreach (var chunk in SystemAPI.QueryBuilder().WithAllRW<MaterialMeshInfoBuffered>().WithOptions(EntityQueryOptions.FilterWriteGroup).Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    using var batcher = m_Queue.Auto(in chunk);

                    var materialMeshAccessor = chunk.GetBufferAccessor(ref MaterialMeshInfoBuffered);

                    for (int entity = 0; entity < chunk.Count; entity++)
                    {
                        var mmb = materialMeshAccessor[entity];
                        for (int element = 0; element < mmb.Length; element++)
                        {
                            batcher.Add(mmb[element].Material, mmb[element].Mesh, entity, element);
                        }
                    }
                }
            }
        }

    }
}
