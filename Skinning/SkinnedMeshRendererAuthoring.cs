using Unity.Entities;
using UnityEngine;

namespace Graphix
{
    public class SkinnedMeshRendererAuthoring : MonoBehaviour
    {
        public SkinAuthoring Skin;
        public Material Material;
    }

    public struct SkinRootEntity : IComponentData
    {
        public Entity Value;
    }

    [WriteGroup(typeof(MaterialMeshBaking))]
    struct SkinnedMaterialMeshInfo : IComponentData { }

    class SkinnedMeshRendererBaker : Baker<SkinnedMeshRendererAuthoring>
    {
        public override void Bake(SkinnedMeshRendererAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var meshRenderer = authoring.GetComponent<SkinnedMeshRenderer>();
            AddComponentObject(entity, new MaterialMeshBaking
            {
                Mesh = meshRenderer.sharedMesh,
                Material = authoring.Material
            });
            AddComponent<SkinnedMaterialMeshInfo>(entity);
            AddComponent(entity, new SkinRootEntity { Value = GetEntity(authoring.Skin, TransformUsageFlags.None) });
        }
    }
}

