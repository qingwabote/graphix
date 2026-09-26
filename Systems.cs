using Bag;
using Unity.Entities;
using Unity.Transforms;

namespace Graphix
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct JointAllocator : ISystem { }

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateAfter(typeof(JointAllocator)), UpdateBefore(typeof(TransformSystemGroup))]
    public partial class AnimationSamplerGroup : ComponentSystemGroup { }

    [UpdateAfter(typeof(AnimationSamplerGroup))]
    public partial struct AnimationTimeStepper : ISystem { }

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateAfter(typeof(AnimationSamplerGroup))]
    public partial struct JointUpdater : ISystem { }

    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial struct Freezer : ISystem { }
}

namespace Unity.Rendering
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [CreateAfter(typeof(BatchGroup))]
    [UpdateInGroup(typeof(PresentationSystemGroup)), UpdateAfter(typeof(BatchGroup))]
    public partial class EntitiesGraphicsSystem : SystemBase { }
}
