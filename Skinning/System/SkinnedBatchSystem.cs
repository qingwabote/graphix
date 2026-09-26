using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;
using Bag;

namespace Graphix
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(BatchGroup))]
    [CreateAfter(typeof(EntitiesGraphicsSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SkinnedBatchSystem : ISystem
    {
        private static readonly int s_JOINTS = Shader.PropertyToID("_JointMap");

        // escape from managed EntitiesGraphicsSystem
        private BatchQueue m_Queue;

        public void OnCreate(ref SystemState state)
        {
            m_Queue = state.World.GetExistingSystemManaged<EntitiesGraphicsSystem>().Queue;
        }

        public void OnUpdate(ref SystemState state)
        {
            var MaterialMeshInfoBuffered = SystemAPI.GetBufferTypeHandle<MaterialMeshInfoBuffered>(true);
            var SkinInfo = SystemAPI.GetComponentTypeHandle<SkinInfo>(true);

            state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

            foreach (var chunk in SystemAPI.QueryBuilder().WithAll<MaterialMeshInfoBuffered, SkinInfo>().Build().ToArchetypeChunkArray(Allocator.Temp))
            {
                using var batcher = m_Queue.Auto(in chunk);

                var materialMeshAccessor = chunk.GetBufferAccessor(ref MaterialMeshInfoBuffered);

                var SkinInfos = chunk.GetNativeArray(ref SkinInfo);
                for (int entity = 0; entity < chunk.Count; entity++)
                {
                    var mmb = materialMeshAccessor[entity];
                    var skin = SkinInfos[entity];
                    var jointMetaHash = skin.JointMeta.GetDataHash();
                    for (int i = 0; i < mmb.Length; i++)
                    {
                        var length = m_Queue.Length;
                        ref var batch = ref batcher.Add(mmb[i].Material, mmb[i].Mesh, entity, i, (int)jointMetaHash);
                        if (m_Queue.Length != length)
                        {
                            var store = PoseCache.Get(skin.JointMeta).GetStore(skin.Baking);
                            store.Update();
                            batch.PropertyTextureBind(s_JOINTS, store.Texture);
                        }
                    }
                }
            }
        }
    }
}
