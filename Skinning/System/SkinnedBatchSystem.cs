using Bastard;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;

namespace Graphix
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(BatchGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SkinnedBatchSystem : ISystem
    {
        private static readonly int s_JOINTS = Shader.PropertyToID("_JointMap");

        private Batcher m_Batcher;

        private Profile.Handle m_Profile;

        public void OnCreate(ref SystemState state)
        {
            m_Batcher = new(Allocator.Persistent);
        }

        unsafe public void OnUpdate(ref SystemState state)
        {
            if (m_Profile.Entry == 0)
            {
                m_Profile = Profile.DefineEntry("SkinBatcher");
            }

            using (m_Profile.Auto())
            {
                var MaterialMeshInfoBuffered = SystemAPI.GetBufferTypeHandle<MaterialMeshInfoBuffered>(true);
                var LocalToWorld = SystemAPI.GetComponentTypeHandle<LocalToWorld>(true);
                var SkinInfo = SystemAPI.GetComponentTypeHandle<SkinInfo>(true);
                var MaterialMeshArray = SystemAPI.ManagedAPI.GetSharedComponentTypeHandle<MaterialMeshArray>();
                var SkinArray = SystemAPI.ManagedAPI.GetSharedComponentTypeHandle<SkinArray>();

                state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

                using var scope = m_Batcher.Auto();

                foreach (var chunk in SystemAPI.QueryBuilder().WithAll<MaterialMeshInfoBuffered, SkinInfo, SkinArray>().Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    var skinArray = chunk.GetSharedComponentManaged(SkinArray, state.EntityManager);
                    var materialMeshArray = chunk.GetSharedComponentIndex(MaterialMeshArray);
                    ref var queue = ref EntitiesGraphicsSystemUnmanaged.GetQueue(materialMeshArray);
                    using var batcher = scope.AutoChunk(ref queue, ref state, in chunk, ref LocalToWorld);

                    var materialMeshAccessor = chunk.GetBufferAccessor(ref MaterialMeshInfoBuffered);

                    var SkinInfos = chunk.GetNativeArray(ref SkinInfo);
                    for (int entity = 0; entity < chunk.Count; entity++)
                    {
                        var mmb = materialMeshAccessor[entity];
                        var mmp = (MaterialMeshInfo*)mmb.GetUnsafeReadOnlyPtr();
                        var skin = SkinInfos[entity];
                        var store = skinArray.GetCurrentStore(skin);
                        for (int i = 0; i < mmb.Length; i++)
                        {
                            var length = queue.Length;
                            var batchIndex = batcher.Add(materialMeshArray, mmp[i], entity, i, skin.Skin);
                            if (queue.Length != length)
                            {
                                store.Update();
                                queue.ElementAt(batchIndex).PropertyTextureBind(s_JOINTS, store.Texture);
                            }
                        }
                    }
                }
            }
        }
    }
}
