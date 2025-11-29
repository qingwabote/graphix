using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Graphix
{
    public struct MaterialMeshArray : ISharedComponentData, IEquatable<MaterialMeshArray>
    {
        static private List<MaterialMeshArray> s_Instances = new();

        static public MaterialMeshArray GetCurrent(EntityManager entityManager)
        {
            s_Instances.Clear();
            entityManager.GetAllUniqueSharedComponentsManaged(s_Instances);
            return s_Instances[1];
        }

        public Material[] Materials;
        public Mesh[] Meshes;

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