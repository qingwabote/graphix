using Unity.Entities;
using Unity.Rendering;
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

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateAfter(typeof(JointUpdater))]
    public partial struct JointUploader : ISystem { }


    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial struct Freezer : ISystem { }

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PresentationSystemGroup)), UpdateBefore(typeof(BatchGroup))]
    public partial struct EntitiesGraphicsSystemUnmanaged : ISystem { }

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PresentationSystemGroup)), UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial class BatchGroup : ComponentSystemGroup { }
}

namespace Unity.Rendering
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class EntitiesGraphicsSystem : SystemBase { }
}