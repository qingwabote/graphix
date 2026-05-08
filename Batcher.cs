using Bastard;
using Unity.Burst;
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

        [BurstCompile]
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
                // make MaterialMeshInfo and MaterialMeshInfoBuffered writable for WriteGroup
                foreach (var chunk in SystemAPI.QueryBuilder().WithAnyRW<MaterialMeshInfo, MaterialMeshInfoBuffered>().WithOptions(EntityQueryOptions.FilterWriteGroup).Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    var materialMeshArray = chunk.GetSharedComponentIndex(MaterialMeshArray);
                    ref var queue = ref EntitiesGraphicsSystemUnmanaged.GetQueue(materialMeshArray);
                    var mp = new MaterialPropertyAccessor(ref state, chunk);
                    var worlds = chunk.GetNativeArray(ref LocalToWorld);

                    if (chunk.Has(ref MaterialMeshInfo))
                    {
                        var mms = chunk.GetNativeArray(ref MaterialMeshInfo);
                        for (ushort entity = 0; entity < chunk.Count; entity++)
                        {
                            scope.Merge(ref queue, materialMeshArray, mms[entity], mp.GetData(entity), worlds.ElementAtRO(entity).Value);
                        }
                    }
                    else if (chunk.Has(ref MaterialMeshInfoBuffered))
                    {
                        var mma = chunk.GetBufferAccessor(ref MaterialMeshInfoBuffered);
                        for (ushort entity = 0; entity < chunk.Count; entity++)
                        {
                            var mmb = mma[entity];
                            var mmp = (MaterialMeshInfo*)mmb.GetUnsafeReadOnlyPtr();
                            for (ushort element = 0; element < mmb.Length; element++)
                            {
                                scope.Merge(ref queue, materialMeshArray, mmp[element], mp.GetData(entity, element), worlds.ElementAtRO(entity).Value);
                            }
                        }
                    }
                }
            }
        }
    }
}
