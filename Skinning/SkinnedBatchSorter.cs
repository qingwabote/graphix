using System;
using Unity.Rendering;
using UnityEngine;

namespace Graphix
{
    public struct SkinnedBatchKey : IEquatable<SkinnedBatchKey>
    {
        public int Material;
        public int Mesh;
        public int Skin;

        public override int GetHashCode()
        {
            return Bastard.HashCode.Combine(Material, Mesh, Skin);
        }

        public bool Equals(SkinnedBatchKey other)
        {
            return Material == other.Material && Mesh == other.Mesh && Skin == other.Skin;
        }
    }

    public struct SkinnedBatchSorter : IBatchSorter<SkinnedBatchKey, SkinInfo>
    {
        private static readonly int s_JOINTS = Shader.PropertyToID("_JointMap");

        public SkinArray SkinArray;

        public SkinnedBatchKey KeyGen(MaterialMeshInfo mm, SkinInfo skin)
        {
            return new SkinnedBatchKey
            {
                Material = mm.Material,
                Mesh = mm.Mesh,
                Skin = skin.Proto
            };
        }

        public void BatchInit(Batch batch, MaterialMeshInfo mm, SkinInfo skin)
        {
            batch.Material = mm.Material;
            batch.Mesh = mm.Mesh;
            batch.PropertyTextureBind(s_JOINTS, SkinArray.GetStore(skin).Texture);
        }
    }
}