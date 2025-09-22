using System;
using Unity.Entities;

namespace Unity.Rendering
{
    public struct MaterialMeshInfo : IComponentData, IEquatable<MaterialMeshInfo>
    {
        /* negative for static and positive for dynamic */
        public int Material;
        public int Mesh;

        public int MaterialID
        {
            get => Material;
            set => Material = value;
        }

        public override int GetHashCode()
        {
            return Bastard.HashCode.Combine(Material, Mesh);
        }

        public bool Equals(MaterialMeshInfo other)
        {
            return Material == other.Material && Mesh == other.Mesh;
        }
    }

    public struct MaterialMeshElement : IBufferElementData
    {
        public int Material;
        public int Mesh;
    }
}