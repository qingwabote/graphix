using Unity.Entities;
using UnityEngine;

namespace Graphix
{
    class MeshRendererBaker : Baker<MeshRenderer>
    {
        public override void Bake(MeshRenderer authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var meshFilter = authoring.GetComponent<MeshFilter>();
            AddComponentObject(entity, new MaterialMeshBaking
            {
                Mesh = meshFilter.sharedMesh,
                Material = authoring.sharedMaterial
            });
        }
    }
}

