using Unity.Entities;
using UnityEngine;

namespace Unity.Rendering
{
    public struct MaterialMeshInfo : IComponentData
    {
        public UnityObjectRef<Material> Material;
        public UnityObjectRef<Mesh> Mesh;

        public UnityObjectRef<Material> MaterialID
        {
            get => Material;
            set => Material = value;
        }

        public UnityObjectRef<Mesh> MeshID
        {
            get => Mesh;
            set => Mesh = value;
        }
    }
}

namespace Graphix
{
    public struct MaterialMeshInfoBuffered : IBufferElementData
    {
        public UnityObjectRef<Material> Material;
        public UnityObjectRef<Mesh> Mesh;
    }
}
