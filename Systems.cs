using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace Graphix
{
    [UpdateInGroup(typeof(TransformSystemGroup))]
    public partial struct SkinnedAnimationFilter : ISystem { }

    [UpdateInGroup(typeof(TransformSystemGroup)), UpdateAfter(typeof(SkinnedAnimationFilter)), UpdateBefore(typeof(LocalToWorldSystem))]
    public partial class AnimationSamplerGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(TransformSystemGroup)), UpdateAfter(typeof(AnimationSamplerGroup))]
    public partial struct AnimationTimeStepper : ISystem { }

    [UpdateInGroup(typeof(TransformSystemGroup)), UpdateAfter(typeof(AnimationSamplerGroup))]
    public partial struct SkinnedAnimationUpdater : ISystem { }

    [UpdateInGroup(typeof(TransformSystemGroup)), UpdateAfter(typeof(SkinnedAnimationUpdater))]
    public partial struct SkinnedAnimationUploader : ISystem { }


    [UpdateInGroup(typeof(LateSimulationSystemGroup)), UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial struct Batcher : ISystem { }

    [UpdateInGroup(typeof(LateSimulationSystemGroup)), UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial struct SkinnedBatcher : ISystem { }
}