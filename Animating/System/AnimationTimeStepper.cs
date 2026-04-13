using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Graphix
{
    [RequireMatchingQueriesForUpdate]
    public partial struct AnimationTimeStepper : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (animation, bingings) in SystemAPI.Query<RefRW<AnimationState>, DynamicBuffer<ClipBinging>>())
            {
                ref var binging = ref bingings.ElementAt(animation.ValueRO.Index);
                var duration = binging.Blob.Value.Duration;
                var time = animation.ValueRO.Time;

                if (time < duration)
                {
                    time += SystemAPI.Time.DeltaTime;
                    time = math.min(time, duration);
                }
                else
                {
                    time = 0f;
                }

                animation.ValueRW.Time = time;
            }
        }
    }
}