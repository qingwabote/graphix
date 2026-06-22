using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace Graphix
{
#if UNITY_EDITOR
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [RequireMatchingQueriesForUpdate]
    public partial struct SkinInfoBaker : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            Dictionary<Skin, int> skin2index = new();
            List<Skin> skins = new();

            EntityCommandBuffer ecb = new(Allocator.Temp);
            foreach (var (info, entity) in SystemAPI.Query<SkinInfoBaking>().WithEntityAccess().WithOptions(EntityQueryOptions.IncludePrefab))
            {
                if (!skin2index.TryGetValue(info.Skin, out var skinIndex))
                {
                    skinIndex = skins.Count;
                    skins.Add(info.Skin);
                    skin2index.Add(info.Skin, skinIndex);
                }

                ecb.AddComponent(entity, new SkinInfo
                {
                    Skin = skinIndex,
                    Baking = info.Baking,
                    JointMeta = info.Skin.JointMeta,
                });
            }

            ecb.AddSharedComponentManaged(
                SystemAPI.QueryBuilder().WithAll<SkinInfoBaking>().WithOptions(EntityQueryOptions.IncludePrefab).Build(),
                new SkinArray(skins.ToArray()),
                EntityQueryCaptureMode.AtPlayback
            );

            ecb.Playback(state.EntityManager);
        }
    }
#endif
}
