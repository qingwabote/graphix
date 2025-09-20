using Unity.Entities;

namespace Graphix
{
    public struct SkinOffset : IComponentData
    {
        public int Value;
    }

    public struct SkinInfo : IComponentData
    {
        public int Proto;
        public bool Baking;
    }
}