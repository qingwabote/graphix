using Unity.Entities;
using UnityEngine;

namespace Graphix
{
    [TemporaryBakingType]
    public class MaterialMeshBaking : IComponentData
    {
        public Mesh Mesh;
        public Material Material;
    }

    [TemporaryBakingType]
    public class MaterialMeshArrayBaking : IComponentData
    {
        public Material[] Materials;
        public Mesh[] Meshes;
    }
}