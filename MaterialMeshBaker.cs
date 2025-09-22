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

            List<Material> materials = new() { null };
            List<Mesh> meshes = new() { null };

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

                ecb.AddComponent(entity, new MaterialMesh
                {
                    Material = -materialIndex,
                    Mesh = -meshIndex
                });
            }
            foreach (var (mma, entity) in SystemAPI.Query<MaterialMeshArrayBaking>().WithEntityAccess().WithOptions(EntityQueryOptions.IncludePrefab))
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

            MaterialMeshArray materialMeshArray = new()
            {
                Materials = materials.ToArray(),
                Meshes = meshes.ToArray(),
                HashCode = 666
            };

            ecb.AddSharedComponentManaged(
                SystemAPI.QueryBuilder().WithAny<MaterialMeshBaking, MaterialMeshArrayBaking>().WithOptions(EntityQueryOptions.IncludePrefab).Build(),
                materialMeshArray,
                EntityQueryCaptureMode.AtPlayback
            );

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}