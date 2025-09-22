using Unity.Entities;
using Unity.Rendering;

namespace Graphix
{
    [MaterialProperty("_JointOffset")]
    public struct SkinOffset : IComponentData
    {
        public float Value;
    }

    public struct SkinInfo : IComponentData
    {
        public int Proto;
        public bool Baking;
    }
}