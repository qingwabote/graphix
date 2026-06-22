using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Graphix
{
    public struct MaterialMeshArray : ISharedComponentData, IEquatable<MaterialMeshArray>
    {
        public Material[] Materials;
        public Mesh[] Meshes;

        public uint4 Hash128;

        public override int GetHashCode()
        {
            return (int)Hash128.x;
        }

        public bool Equals(MaterialMeshArray other)
        {
            return math.all(Hash128 == other.Hash128);
        }

#if UNITY_EDITOR
        internal MaterialMeshArray(Material[] materials, Mesh[] meshes)
        {
            Materials = materials;
            Meshes = meshes;

            Hash128 = uint4.zero;
            Hash128 = ComputeHash128();
        }

        private uint4 ComputeHash128()
        {
            var hash = new Unity.Collections.xxHash3.StreamingState(false);

            hash.Update(Materials.Length);
            for (int i = 1; i < Materials.Length; ++i)
                Bastard.AssetHash.Update(ref hash, Materials[i]);

            hash.Update(Meshes.Length);
            for (int i = 1; i < Meshes.Length; ++i)
                Bastard.AssetHash.Update(ref hash, Meshes[i]);

            uint4 H = hash.DigestHash128();

            // Make sure the hash is never exactly zero, to keep zero as a null value
            if (math.all(H == uint4.zero))
                return new uint4(1, 0, 0, 0);

            return H;
        }
#endif
    }
}