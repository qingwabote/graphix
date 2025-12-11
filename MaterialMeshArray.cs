using System;
using Unity.Entities;
using UnityEngine;

namespace Graphix
{
    public struct MaterialMeshArray : ISharedComponentData, IEquatable<MaterialMeshArray>
    {
        public Material[] Materials;
        public Mesh[] Meshes;

        [SerializeField]
        private int m_HashCode;

        internal MaterialMeshArray(Material[] materials, Mesh[] meshes, int hashCode)
        {
            Materials = materials;
            Meshes = meshes;
            m_HashCode = hashCode;
        }

        public override int GetHashCode()
        {
            return m_HashCode;
        }

        public bool Equals(MaterialMeshArray other)
        {
            return m_HashCode == other.m_HashCode;
        }
    }
}