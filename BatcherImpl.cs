using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace Graphix
{
    public unsafe interface IBatchSorter<T>
    {
        T Key(MaterialMeshInfo mm, int entity);
        void Init(Batch batch, MaterialMeshInfo mm, int entity);
    }

    public unsafe struct BatchSorter : IBatchSorter<MaterialMeshInfo>
    {
        public MaterialMeshInfo Key(MaterialMeshInfo mm, int entity)
        {
            return mm;
        }

        public void Init(Batch batch, MaterialMeshInfo mm, int entity)
        {
            batch.Material = mm.Material;
            batch.Mesh = mm.Mesh;
        }
    }

    public unsafe class BatcherImpl<TKey, TSorter> where TSorter : IBatchSorter<TKey>
    {
        private readonly Dictionary<TKey, int> m_Cache = new();

        public TSorter Sorter = default;

        UnsafeList<MaterialProperty>.ReadOnly Properties;
        void** PropertyData = null;

        public void BeginChunk(ref SystemState state, ArchetypeChunk chunk)
        {
            Properties = MaterialProperty.Get(chunk.Archetype);
            PropertyData = (void**)UnsafeUtility.Malloc(sizeof(void*) * Properties.Length, UnsafeUtility.AlignOf<IntPtr>(), Allocator.Temp);
            for (int i = 0; i < Properties.Length; i++)
            {
                var property = Properties.Ptr[i];
                ref var handle = ref MaterialProperty.Handles[property.Type];
                handle.Update(ref state);
                PropertyData[i] = chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref handle, property.Size).GetUnsafeReadOnlyPtr();
            }
        }

        public void Add(MaterialMeshInfo mm, in LocalToWorld world, int entity)
        {
            Batch batch;
            TKey key = Sorter.Key(mm, entity);
            if (m_Cache.TryGetValue(key, out int index))
            {
                batch = EntitiesGraphicsSystem.Queue.Data[index];
            }
            else
            {
                m_Cache.Add(key, EntitiesGraphicsSystem.Queue.Count);
                batch = EntitiesGraphicsSystem.Queue.Push();
                Sorter.Init(batch, mm, entity);

                for (int i = 0; i < Properties.Length; i++)
                {
                    var property = Properties.Ptr[i];
                    if (property.Size == sizeof(float))
                    {
                        batch.PropertyFloatAcquire(property.Name);
                    }
                    else
                    {
                        batch.PropertyVectorAcquire(property.Name);
                    }
                }
            }

            for (int i = 0; i < Properties.Length; i++)
            {
                var property = Properties.Ptr[i];
                if (property.Size == sizeof(float))
                {
                    var data = (float*)PropertyData[i];
                    batch.PropertyFloatAdd(property.Name, data[entity]);
                }
                else
                {
                    var data = (float4*)PropertyData[i];
                    batch.PropertyVectorAdd(property.Name, data[entity]);
                }
            }
            batch.Worlds.Add(world.Value);
            batch.Count++;
        }

        public void EndChunk()
        {
            UnsafeUtility.Free(PropertyData, Allocator.Temp);
            PropertyData = null;
        }

        public void Clear()
        {
            m_Cache.Clear();
        }
    }
}