using Bastard;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;

namespace Graphix
{
    public partial struct SkinnedBatcher : ISystem
    {
        private static BatcherImpl<SkinnedBatchKey, SkinnedBatchSorter> s_Batcher = new();

        private int m_BatchEntry;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SkinArray>();

            m_BatchEntry = Profile.DefineEntry("SkinnedBatch");
        }

        unsafe public void OnUpdate(ref SystemState state)
        {
            using (new Profile.Scope(m_BatchEntry))
            {
                var MaterialMeshElement = SystemAPI.GetBufferTypeHandle<MaterialMeshElement>(true);
                MaterialMeshElement.Update(ref state);
                var LocalToWorld = SystemAPI.GetComponentTypeHandle<LocalToWorld>(true);
                LocalToWorld.Update(ref state);
                var SkinInfo = SystemAPI.GetComponentTypeHandle<SkinInfo>(true);
                SkinInfo.Update(ref state);

                s_Batcher.Sorter.SkinArray = SkinArray.GetInstance(ref state);

                state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

                foreach (var chunk in SystemAPI.QueryBuilder().WithAll<MaterialMeshElement>().Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    s_Batcher.BeginChunk(ref state, chunk);
                    var mma = chunk.GetBufferAccessor(ref MaterialMeshElement);
                    var worlds = chunk.GetNativeArray(ref LocalToWorld);
                    s_Batcher.Sorter.SkinInfos = chunk.GetNativeArray(ref SkinInfo);
                    for (int entity = 0; entity < chunk.Count; entity++)
                    {
                        var mmb = mma[entity];
                        var mmp = (MaterialMeshInfo*)mmb.GetUnsafeReadOnlyPtr();
                        for (int i = 0; i < mmb.Length; i++)
                        {
                            s_Batcher.Add(mmp[i], worlds.ElementAtRO(entity), entity);
                        }
                    }
                    s_Batcher.EndChunk();
                }
                s_Batcher.Clear();
            }
        }
    }
}