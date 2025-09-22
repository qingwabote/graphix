using System;
using Unity.Collections;
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

    public unsafe struct SkinnedBatchSorter : IBatchSorter<SkinnedBatchKey>
    {
        private static readonly int s_JOINTS = Shader.PropertyToID("_JointMap");

        public SkinArray SkinArray;
        public NativeArray<SkinInfo> SkinInfos;

        public SkinnedBatchKey Key(MaterialMesh mm, int entity)
        {
            return new SkinnedBatchKey
            {
                Material = mm.Material,
                Mesh = mm.Mesh,
                Skin = SkinInfos[entity].Proto
            };
        }

        public void Init(Batch batch, MaterialMesh mm, int entity)
        {
            batch.Material = mm.Material;
            batch.Mesh = mm.Mesh;
            batch.PropertyTextureBind(s_JOINTS, SkinArray.GetStore(SkinInfos[entity]).Texture);
        }
    }
}