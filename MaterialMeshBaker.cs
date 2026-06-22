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

            EntityCommandBuffer ecb = new(Allocator.Temp);
            foreach (var (mm, entity) in SystemAPI.Query<MaterialMeshBaking>().WithNone<MaterialMeshBufferedBaking>().WithEntityAccess().WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.FilterWriteGroup))
            {
                if (!material2index.TryGetValue(mm.Material, out var materialIndex))
                {
                    materialIndex = materials.Count;
                    materials.Add(mm.Material);
                    material2index.Add(mm.Material, materialIndex);
                }

                if (!mesh2index.TryGetValue(mm.Mesh, out var meshIndex))
                {
                    meshIndex = meshes.Count;
                    meshes.Add(mm.Mesh);
                    mesh2index.Add(mm.Mesh, meshIndex);
                }

                ecb.AddComponent(entity, new MaterialMeshInfo
                {
                    Material = -materialIndex,
                    Mesh = -meshIndex
                });
            }
            foreach (var (mmb, entity) in SystemAPI.Query<MaterialMeshBufferedBaking>().WithEntityAccess().WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
            {
                var buffer = ecb.AddBuffer<MaterialMeshInfoBuffered>(entity);
                var count = mmb.Materials.Length;
                for (int i = 0; i < count; i++)
                {
                    var material = mmb.Materials[i];
                    var mesh = mmb.Meshes[i];

                    if (!material2index.TryGetValue(material, out var materialIndex))
                    {
                        materialIndex = materials.Count;
                        materials.Add(material);
                        material2index.Add(material, materialIndex);
                    }

                    if (!mesh2index.TryGetValue(mesh, out var meshIndex))
                    {
                        meshIndex = meshes.Count;
                        meshes.Add(mesh);
                        mesh2index.Add(mesh, meshIndex);
                    }

                    buffer.Add(new() { Material = -materialIndex, Mesh = -meshIndex });
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
