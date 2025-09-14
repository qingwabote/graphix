using System;
using System.Collections.Generic;
using System.Linq;
using Bastard;
using Budget;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Graphix
{
    public partial struct Batcher : ISystem
    {
        static private TransientPool<List<Vector4>> s_VectorPool;

        private int m_BatchEntry;

        public void OnCreate(ref SystemState state)
        {
            s_VectorPool = new();

            MaterialProperty.Initialize(ref state);

            m_BatchEntry = Profile.DefineEntry("Batch");
        }

        unsafe public void OnUpdate(ref SystemState state)
        {
            using (new Profile.Scope(m_BatchEntry))
            {
                var MaterialMeshInfo = SystemAPI.GetComponentTypeHandle<MaterialMeshInfo>();
                MaterialMeshInfo.Update(ref state);
                var LocalToWorld = SystemAPI.GetComponentTypeHandle<LocalToWorld>(true);
                LocalToWorld.Update(ref state);

                state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

                // make MaterialMeshInfo RefRW for WriteGroup
                foreach (var chunk in SystemAPI.QueryBuilder().WithAllRW<MaterialMeshInfo>().WithOptions(EntityQueryOptions.FilterWriteGroup).Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    var properties = MaterialProperty.Get(chunk.Archetype);
                    var propertyData = stackalloc void*[properties.Length];
                    for (int i = 0; i < properties.Length; i++)
                    {
                        var property = properties.Ptr[i];
                        ref var handle = ref MaterialProperty.Handles[property.Type];
                        MaterialProperty.Handles[property.Type].Update(ref state);
                        propertyData[i] = chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref handle, property.Size).GetUnsafeReadOnlyPtr();
                    }

                    var mms = chunk.GetNativeArray(ref MaterialMeshInfo);
                    var worlds = chunk.GetNativeArray(ref LocalToWorld);
                    for (int entity = 0; entity < chunk.Count; entity++)
                    {
                        var mm = mms[entity];
                        if (Batch.Get(HashCode.Combine(mm.Mesh, mm.Material), out Batch batch))
                        {
                            batch.Material = mm.Material;
                            batch.Mesh = mm.Mesh;
                            for (int i = 0; i < properties.Length; i++)
                            {
                                var property = properties.Ptr[i];
                                var vectors = s_VectorPool.Get();
                                vectors.Clear();
                                batch.MaterialProperty.Vectors.Add(property.Name, vectors);
                            }
                        }
                        for (int i = 0; i < properties.Length; i++)
                        {
                            var property = properties.Ptr[i];
                            batch.MaterialProperty.Vectors.TryGetValue(property.Name, out var vectors);
                            float4* data = (float4*)propertyData[i];
                            vectors.Add(data[entity]);
                        }

                        batch.InstanceWorlds.Add(worlds.ElementAtRO(entity).Value);
                        batch.InstanceCount++;
                    }
                }
            }
        }
    }
}