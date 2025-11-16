using Bastard;
using Unity.Entities;
using Unity.Transforms;

namespace Graphix
{
    struct TransformProfiler
    {
        static public int Transform = Profile.DefineEntry("Transform");
    }

    [UpdateInGroup(typeof(TransformSystemGroup)), UpdateBefore(typeof(LocalToWorldSystem)), UpdateAfter(typeof(ParentSystem))]
    partial struct BeforeLocalToWorldSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            Profile.Begin(TransformProfiler.Transform);
        }
    }

    [UpdateInGroup(typeof(TransformSystemGroup)), UpdateAfter(typeof(LocalToWorldSystem))]
    partial struct AfterLocalToWorldSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            Profile.End(TransformProfiler.Transform);
        }
    }
}