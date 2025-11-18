using Graphix;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
public partial struct TargetTransformBaker : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new(Allocator.Temp);
        foreach (var nodes in SystemAPI.Query<DynamicBuffer<ChannelTarget>>().WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var entity = nodes[i].Value;
                if (SystemAPI.HasComponent<LocalTransform>(entity))
                {
                    continue;
                }
                ecb.AddComponent<LocalTransform>(entity);
            }
        }
        ecb.Playback(state.EntityManager);
    }
}