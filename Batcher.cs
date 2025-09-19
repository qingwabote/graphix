using Bastard;
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
        private int m_BatchEntry;

        public void OnCreate(ref SystemState state)
        {
            MaterialProperty.Initialize(ref state);

            m_BatchEntry = Profile.DefineEntry("Batch");
        }

        unsafe public void OnUpdate(ref SystemState state)
        {
            using (new Profile.Scope(m_BatchEntry))
            {
                var MaterialMeshInfo = SystemAPI.GetComponentTypeHandle<MaterialMesh>();
                MaterialMeshInfo.Update(ref state);
                var LocalToWorld = SystemAPI.GetComponentTypeHandle<LocalToWorld>(true);
                LocalToWorld.Update(ref state);

                state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

                // make MaterialMeshInfo RefRW for WriteGroup
                foreach (var chunk in SystemAPI.QueryBuilder().WithAllRW<MaterialMesh>().WithOptions(EntityQueryOptions.FilterWriteGroup).Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    var properties = MaterialProperty.Get(chunk.Archetype);
                    var propertyData = stackalloc void*[properties.Length];
                    for (int i = 0; i < properties.Length; i++)
                    {
                        var property = properties.Ptr[i];
                        ref var handle = ref MaterialProperty.Handles[property.Type];
                        handle.Update(ref state);
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
                                batch.PropertyVectorAcquire(property.Name);
                            }
                        }
                        for (int i = 0; i < properties.Length; i++)
                        {
                            var property = properties.Ptr[i];
                            var data = (float4*)propertyData[i];
                            batch.PropertyVectorAdd(property.Name, data[entity]);
                        }

                        batch.Worlds.Add(worlds.ElementAtRO(entity).Value);
                        batch.Count++;
                    }
                }
            }
        }
    }
}