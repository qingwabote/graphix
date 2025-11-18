using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Graphix
{
    public partial struct AnimationTimeStepper : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AnimationState>();
        }

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