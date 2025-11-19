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
                if (!SystemAPI.HasComponent<LocalTransform>(entity))
                {
                    ecb.AddComponent(entity, transforms.ElementAt(i).Value);
                }
            }
        }
        foreach (var nodes in SystemAPI.Query<DynamicBuffer<SkinNode>>().WithNone<TransformBaking>().WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var entity = nodes[i].Target;
                // Ensure LocalTransform exists. I don't know why, but it happens.
                if (!SystemAPI.HasComponent<LocalTransform>(entity))
                {
                    ecb.AddComponent<LocalTransform>(entity);
                }

            }
        }
        ecb.Playback(state.EntityManager);
    }
}