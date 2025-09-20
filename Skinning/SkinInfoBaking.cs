using Unity.Entities;

namespace Graphix
{
    [TemporaryBakingType]
    public class SkinInfoBaking : IComponentData
    {
        public Skin Proto;
        public bool Baking;
    }
}