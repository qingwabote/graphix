using Bastard;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace Graphix
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(BatchGroup))]
    [CreateAfter(typeof(RenderContextSystem))]
    [RequireMatchingQueriesForUpdate]
    public unsafe partial struct BatchSystem : ISystem
    {
        private static readonly Profile.Handle s_Profile = Profile.DefineEntry("Batcher");

        private Batcher m_Batcher;

        public void OnCreate(ref SystemState state)
        {
            ref var context = ref state.World.Unmanaged.GetUnsafeSystemRef<RenderContextSystem>(state.World.GetExistingSystem<RenderContextSystem>());
            m_Batcher = new((RenderContextSystem*)UnsafeUtility.AddressOf(ref context), Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            m_Batcher.Dispose();
        }

        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using (s_Profile.Auto())
            {

                var MaterialMeshInfo = SystemAPI.GetComponentTypeHandle<MaterialMeshInfo>(true);
                var MaterialMeshInfoBuffered = SystemAPI.GetBufferTypeHandle<MaterialMeshInfoBuffered>(true);

                state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

                using var scope = m_Batcher.Auto();

                // make MaterialMeshInfo writable for WriteGroup
                foreach (var chunk in SystemAPI.QueryBuilder().WithAllRW<MaterialMeshInfo>().WithOptions(EntityQueryOptions.FilterWriteGroup).Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    using var batcher = scope.AutoChunk(in chunk);

                    var mms = chunk.GetNativeArray(ref MaterialMeshInfo);
                    for (int entity = 0; entity < chunk.Count; entity++)
                    {
                        batcher.Add(mms[entity], entity);
                    }
                }

                // make MaterialMeshInfoBuffered writable for WriteGroup
                foreach (var chunk in SystemAPI.QueryBuilder().WithAllRW<MaterialMeshInfoBuffered>().WithOptions(EntityQueryOptions.FilterWriteGroup).Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    using var batcher = scope.AutoChunk(in chunk);

                    var materialMeshAccessor = chunk.GetBufferAccessor(ref MaterialMeshInfoBuffered);

                    for (int entity = 0; entity < chunk.Count; entity++)
                    {
                        var mmb = materialMeshAccessor[entity];
                        var mmp = (MaterialMeshInfo*)mmb.GetUnsafeReadOnlyPtr();
                        for (int element = 0; element < mmb.Length; element++)
                        {
                            batcher.Add(mmp[element], entity, element);
                        }
                    }
                }
            }
        }

    }
}
