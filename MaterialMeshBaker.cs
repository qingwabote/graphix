using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace Graphix
{
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
            foreach (var (mm, entity) in SystemAPI.Query<MaterialMeshBaking>().WithEntityAccess().WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
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
            foreach (var (mma, entity) in SystemAPI.Query<MaterialMeshArrayBaking>().WithEntityAccess().WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
            {
                var mmb = ecb.AddBuffer<MaterialMeshElement>(entity);
                var count = mma.Materials.Length;
                for (int i = 0; i < count; i++)
                {
                    var material = mma.Materials[i];
                    var mesh = mma.Meshes[i];

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

                    mmb.Add(new MaterialMeshElement
                    {
                        Material = -materialIndex,
                        Mesh = -meshIndex
                    });
                }
            }

            List<SceneSection> scenes = new();
            state.EntityManager.GetAllUniqueSharedComponentsManaged(scenes);

            ecb.AddSharedComponentManaged(
                SystemAPI.QueryBuilder().WithAny<MaterialMeshBaking, MaterialMeshArrayBaking>().WithOptions(EntityQueryOptions.IncludePrefab).Build(),
                new MaterialMeshArray(materials.ToArray(), meshes.ToArray(), scenes[1].SceneGUID.GetHashCode()),
                EntityQueryCaptureMode.AtPlayback
            );

            ecb.Playback(state.EntityManager);
        }
    }
}