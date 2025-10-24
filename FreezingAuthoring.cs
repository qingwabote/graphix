using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Graphix
{
    public struct Freezing : IComponentData { }

    [TemporaryBakingType]
    public struct FreezingBaking : IBufferElementData
    {
        public Entity Value;
    }

    public class FreezingAuthoring : MonoBehaviour
    {
        class Baker : Baker<FreezingAuthoring>
        {
            public override void Bake(FreezingAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.None);
                var buf = AddBuffer<FreezingBaking>(entity);
                foreach (var transform in authoring.GetComponentsInChildren<Transform>(true))
                {
                    buf.Add(new FreezingBaking { Value = GetEntity(transform, TransformUsageFlags.None) });
                }
            }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct FreezingBaker : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new(Allocator.Temp);
            foreach (var buf in SystemAPI.Query<DynamicBuffer<FreezingBaking>>().WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
            {
                foreach (var item in buf)
                {
                    ecb.AddComponent<Freezing>(item.Value);
                }
            }
            ecb.Playback(state.EntityManager);
        }
    }

    [UpdateInGroup(typeof(TransformSystemGroup)), UpdateAfter(typeof(LocalToWorldSystem))]
    public partial struct Freezer : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.RemoveComponent(SystemAPI.QueryBuilder().WithAll<LocalTransform, Freezing>().Build(), new ComponentTypeSet(typeof(LocalTransform), typeof(Freezing)));
        }
    }
}