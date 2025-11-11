using Graphix;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
public partial struct SkinNodeSlimmer : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new(Allocator.Temp);
        foreach (var nodes in SystemAPI.Query<DynamicBuffer<SkinNode>>().WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var entity = nodes[i].Target;
                ecb.RemoveComponent<Parent>(entity);
                ecb.RemoveComponent<LocalToWorld>(entity);
            }
        }
        ecb.Playback(state.EntityManager);
    }
}