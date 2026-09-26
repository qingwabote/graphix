using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace Graphix
{
#if UNITY_EDITOR
    class MeshRendererBaker : Baker<MeshRenderer>
    {
        public override void Bake(MeshRenderer authoring)
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null) { return; }

            var entity = GetEntity(TransformUsageFlags.Renderable);
            AddComponent(entity, new MaterialMeshInfo
            {
                Mesh = meshFilter.sharedMesh,
                Material = authoring.sharedMaterial
            });
        }
    }
#endif
}
