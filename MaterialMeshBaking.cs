using Unity.Entities;
using UnityEngine;

namespace Graphix
{
    [BakingType]
    public class MaterialMeshBaking : IComponentData
    {
        public Mesh Mesh;
        public Material Material;
    }

    [BakingType]
    public class MaterialMeshBufferedBaking : IComponentData
    {
        public Material[] Materials;
        public Mesh[] Meshes;
    }
}