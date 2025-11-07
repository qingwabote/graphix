using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace Graphix
{
    [UpdateInGroup(typeof(TransformSystemGroup))]
    public partial struct JointAllocator : ISystem { }

    [UpdateInGroup(typeof(TransformSystemGroup)), UpdateAfter(typeof(JointAllocator)), UpdateBefore(typeof(LocalToWorldSystem))]
    public partial class AnimationSamplerGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(TransformSystemGroup)), UpdateAfter(typeof(AnimationSamplerGroup))]
    public partial struct AnimationTimeStepper : ISystem { }

    [UpdateInGroup(typeof(TransformSystemGroup)), UpdateAfter(typeof(AnimationSamplerGroup))]
    public partial struct JointUpdater : ISystem { }

    [UpdateInGroup(typeof(TransformSystemGroup)), UpdateAfter(typeof(JointUpdater))]
    public partial struct JointUploader : ISystem { }


    [UpdateInGroup(typeof(LateSimulationSystemGroup)), UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial struct Batcher : ISystem { }

    [UpdateInGroup(typeof(LateSimulationSystemGroup)), UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial struct SkinnedBatcher : ISystem { }
}