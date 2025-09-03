using Unity.Entities;
using UnityEngine;

namespace Graphix
{
    public class MeshRendererAuthoring : MonoBehaviour { }

    class MeshRendererBaker : Baker<MeshRendererAuthoring>
    {
        public override void Bake(MeshRendererAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var meshFilter = authoring.GetComponent<MeshFilter>();
            var meshRenderer = authoring.GetComponent<MeshRenderer>();
            AddComponentObject(entity, new MaterialMeshBaking
            {
                Mesh = meshFilter.sharedMesh,
                Material = meshRenderer.sharedMaterial
            });
        }
    }
}

