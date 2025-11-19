using Graphix;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
[UpdateInGroup(typeof(PostBakingSystemGroup))]
public partial struct TargetStripper : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new(Allocator.Temp);
        foreach (var nodes in SystemAPI.Query<DynamicBuffer<ChannelTarget>>().WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                ref var target = ref nodes.ElementAt(i);
                if (!SystemAPI.HasComponent<LocalTransform>(target.Value))
                {
                    target.Value = Entity.Null;
                }
            }
        }
        ecb.Playback(state.EntityManager);
    }
}