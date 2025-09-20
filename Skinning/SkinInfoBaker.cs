using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace Graphix
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct SkinInfoBaker : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            Dictionary<Skin, int> skin2index = new();
            List<Skin> skins = new();

            EntityCommandBuffer ecb = new(Allocator.TempJob);
            foreach (var (info, entity) in SystemAPI.Query<SkinInfoBaking>().WithEntityAccess().WithOptions(EntityQueryOptions.IncludePrefab))
            {
                if (!skin2index.TryGetValue(info.Proto, out var skinIndex))
                {
                    skinIndex = skins.Count;
                    skins.Add(info.Proto);
                    skin2index.Add(info.Proto, skinIndex);
                }

                ecb.AddComponent(entity, new SkinInfo
                {
                    Proto = skinIndex,
                    Baking = info.Baking
                });
            }

            SkinArray skinArray = new()
            {
                Data = skins.ToArray(),
                HashCode = 666
            };
            ecb.AddSharedComponentManaged(
                SystemAPI.QueryBuilder().WithAny<SkinInfoBaking>().WithOptions(EntityQueryOptions.IncludePrefab).Build(),
                skinArray,
                EntityQueryCaptureMode.AtPlayback
            );

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}