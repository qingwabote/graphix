using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Graphix
{
    public struct MaterialMeshArray : ISharedComponentData, IEquatable<MaterialMeshArray>
    {
        static private MaterialMeshArray s_Instance;

        static public MaterialMeshArray GetInstance(EntityManager entityManager)
        {
            if (s_Instance.HashCode != 0)
            {
                return s_Instance;
            }

            List<MaterialMeshArray> list = new();
            entityManager.GetAllUniqueSharedComponentsManaged(list);
            s_Instance = list[1]; // 0 is always default
            return s_Instance;
        }

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