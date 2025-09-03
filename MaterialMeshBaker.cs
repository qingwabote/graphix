using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Graphix
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct MaterialMeshBaker : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            Dictionary<Material, int> material2index = new();
            Dictionary<Mesh, int> mesh2index = new();

            List<Material> materials = new();
            List<Mesh> meshes = new();

            EntityCommandBuffer ecb = new(Allocator.TempJob);
            foreach (var (mm, entity) in SystemAPI.Query<MaterialMeshBaking>().WithEntityAccess().WithOptions(EntityQueryOptions.IncludePrefab))
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
                    Material = materialIndex,
                    Mesh = meshIndex
                });
            }

            MaterialMeshArray materialMeshArray = new()
            {
                Materials = materials.ToArray(),
                Meshes = meshes.ToArray(),
                HashCode = 666
            };
            foreach (var (mm, entity) in SystemAPI.Query<MaterialMeshBaking>().WithEntityAccess().WithOptions(EntityQueryOptions.IncludePrefab))
            {
                ecb.AddSharedComponentManaged(entity, materialMeshArray);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}