using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Graphix
{
    public struct MaterialMeshArray : ISharedComponentData, IEquatable<MaterialMeshArray>
    {
        public Material[] Materials;
        public Mesh[] Meshes;

        public int HashCode;

        public override int GetHashCode()
        {
            return HashCode;
        }

        public bool Equals(MaterialMeshArray other)
        {
            return HashCode == other.HashCode;
        }
    }
}