using Unity.Entities;

namespace Graphix
{
    public struct MaterialMeshInfo : IComponentData
    {
        public int Material;
        public int Mesh;
    }
}