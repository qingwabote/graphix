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
    public partial struct BatchSystem : ISystem
    {
        private Batcher m_Batcher;

        private Profile.Handle m_Profile;

        public void OnCreate(ref SystemState state)
        {
            m_Batcher = new(Allocator.Persistent);
        }

        // [BurstCompile]
        public unsafe void OnUpdate(ref SystemState state)
        {
            if (m_Profile.Entry == 0)
            {
                m_Profile = Profile.DefineEntry("Batcher");
            }

            using (m_Profile.Auto())
            {
                var MaterialMeshInfo = SystemAPI.GetComponentTypeHandle<MaterialMeshInfo>(true);
                var MaterialMeshInfoBuffered = SystemAPI.GetBufferTypeHandle<MaterialMeshInfoBuffered>(true);
                var LocalToWorld = SystemAPI.GetComponentTypeHandle<LocalToWorld>(true);
                var MaterialMeshArray = SystemAPI.ManagedAPI.GetSharedComponentTypeHandle<MaterialMeshArray>();

                state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

                using var scope = m_Batcher.Auto();

                // make MaterialMeshInfo writable for WriteGroup
                foreach (var chunk in SystemAPI.QueryBuilder().WithAllRW<MaterialMeshInfo>().WithOptions(EntityQueryOptions.FilterWriteGroup).Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    var materialMeshArray = chunk.GetSharedComponentIndex(MaterialMeshArray);
                    ref var queue = ref EntitiesGraphicsSystemUnmanaged.GetQueue(materialMeshArray);
                    using var batcher = scope.AutoChunk(ref queue, ref state, in chunk, ref LocalToWorld);

                    var mms = chunk.GetNativeArray(ref MaterialMeshInfo);
                    for (int entity = 0; entity < chunk.Count; entity++)
                    {
                        batcher.Add(materialMeshArray, mms[entity], entity);
                    }
                }

                // make MaterialMeshInfoBuffered writable for WriteGroup
                foreach (var chunk in SystemAPI.QueryBuilder().WithAllRW<MaterialMeshInfoBuffered>().WithOptions(EntityQueryOptions.FilterWriteGroup).Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    var materialMeshArray = chunk.GetSharedComponentIndex(MaterialMeshArray);
                    ref var queue = ref EntitiesGraphicsSystemUnmanaged.GetQueue(materialMeshArray);
                    using var batcher = scope.AutoChunk(ref queue, ref state, in chunk, ref LocalToWorld);

                    var materialMeshAccessor = chunk.GetBufferAccessor(ref MaterialMeshInfoBuffered);

                    for (int entity = 0; entity < chunk.Count; entity++)
                    {
                        var mmb = materialMeshAccessor[entity];
                        var mmp = (MaterialMeshInfo*)mmb.GetUnsafeReadOnlyPtr();
                        for (int element = 0; element < mmb.Length; element++)
                        {
                            batcher.Add(materialMeshArray, mmp[element], entity, element);
                        }
                    }
                }
            }
        }

    }
}
