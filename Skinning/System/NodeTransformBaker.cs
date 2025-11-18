using Graphix;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
public partial struct NodeTransformBaker : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new(Allocator.Temp);
        foreach (var (nodes, transforms) in SystemAPI.Query<DynamicBuffer<SkinNode>, DynamicBuffer<TransformBaking>>().WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var entity = nodes[i].Target;
                if (SystemAPI.HasComponent<LocalTransform>(entity))
                {
                    continue;
                }
                ecb.AddComponent(entity, transforms.ElementAt(i).Value);
            }
        }
        ecb.Playback(state.EntityManager);
    }
}