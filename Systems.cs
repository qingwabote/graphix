using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace Graphix
{
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct JointAllocator : ISystem { }

    [UpdateAfter(typeof(JointAllocator)), UpdateBefore(typeof(TransformSystemGroup))]
    public partial class AnimationSamplerGroup : ComponentSystemGroup { }
    [UpdateAfter(typeof(AnimationSamplerGroup))]
    public partial struct AnimationTimeStepper : ISystem { }

    [UpdateAfter(typeof(AnimationSamplerGroup))]
    public partial struct JointUpdater : ISystem { }
    [UpdateAfter(typeof(JointUpdater))]
    public partial struct JointUploader : ISystem { }


    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial struct Freezer : ISystem { }


    [UpdateInGroup(typeof(PresentationSystemGroup)), UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial class BatchGroup : ComponentSystemGroup { }
}

namespace Unity.Rendering
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class EntitiesGraphicsSystem : SystemBase { }
}