using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace Graphix
{
#if UNITY_EDITOR
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [RequireMatchingQueriesForUpdate]
    public partial struct MaterialMeshBaker : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            Dictionary<Material, int> material2index = new();
            Dictionary<Mesh, int> mesh2index = new();

            List<Material> materials = new() { null };
            List<Mesh> meshes = new() { null };

            (int Material, int Mesh) AddMaterialMesh(Material material, Mesh mesh)
            {
                int materialIndex = 0;
                if (material != null && !material2index.TryGetValue(material, out materialIndex))
                {
                    materialIndex = materials.Count;
                    materials.Add(material);
                    material2index.Add(material, materialIndex);
                }

                int meshIndex = 0;
                if (mesh != null && !mesh2index.TryGetValue(mesh, out meshIndex))
                {
                    meshIndex = meshes.Count;
                    meshes.Add(mesh);
                    mesh2index.Add(mesh, meshIndex);
                }

                return (materialIndex, meshIndex);
            }

            EntityCommandBuffer ecb = new(Allocator.Temp);
            foreach (var (mm, entity) in SystemAPI.Query<MaterialMeshBaking>().WithNone<MaterialMeshBufferedBaking>().WithEntityAccess().WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.FilterWriteGroup))
            {
                var index = AddMaterialMesh(mm.Material, mm.Mesh);
                ecb.AddComponent(entity, new MaterialMeshInfo
                {
                    Material = -index.Material,
                    Mesh = -index.Mesh
                });
            }
            foreach (var (mmb, entity) in SystemAPI.Query<MaterialMeshBufferedBaking>().WithEntityAccess().WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
            {
                var buffer = ecb.AddBuffer<MaterialMeshInfoBuffered>(entity);
                var count = mmb.Materials.Length;
                for (int i = 0; i < count; i++)
                {
                    var index = AddMaterialMesh(mmb.Materials[i], mmb.Meshes[i]);
                    buffer.Add(new()
                    {
                        Material = -index.Material,
                        Mesh = -index.Mesh
                    });
                }
            }

            ecb.AddSharedComponentManaged(
                SystemAPI.QueryBuilder().WithAny<MaterialMeshBaking, MaterialMeshBufferedBaking>().WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities).Build(),
                new MaterialMeshArray(materials.ToArray(), meshes.ToArray()),
                EntityQueryCaptureMode.AtPlayback
            );

            ecb.Playback(state.EntityManager);
        }
    }
#endif
}
