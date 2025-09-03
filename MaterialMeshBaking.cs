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
}