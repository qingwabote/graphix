using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Graphix
{
    public partial struct AnimationTimeStepper : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (animation, clipBingings) in SystemAPI.Query<RefRW<AnimationState>, DynamicBuffer<ClipBinging>>())
            {
                ref var clipBinging = ref clipBingings.ElementAt(animation.ValueRO.ClipIndex);
                var time = animation.ValueRW.Time;
                var duration = clipBinging.Duration;

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