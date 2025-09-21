using System;
using Bastard;
using Unity.Entities;

namespace Graphix
{
    public struct MaterialMesh : IComponentData, IEquatable<MaterialMesh>
    {
        public int Material;
        public int Mesh;

        public override int GetHashCode()
        {
            return Bastard.HashCode.Combine(Material, Mesh);
        }

        public bool Equals(MaterialMesh other)
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