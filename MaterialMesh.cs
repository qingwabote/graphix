using Unity.Entities;

namespace Graphix
{
    public struct MaterialMesh : IComponentData
    {
        public int Material;
        public int Mesh;
    }

    public struct MaterialMeshElement : IBufferElementData
    {
        public int Material;
        public int Mesh;
    }
}