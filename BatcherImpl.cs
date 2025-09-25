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

    public unsafe class BatcherImpl<TKey, TSorter> where TKey : unmanaged, IEquatable<TKey> where TSorter : IBatchSorter<TKey>
    {
        struct KeyWithProperty : IEquatable<KeyWithProperty>
        {
            public TKey Key;
            public UnsafeList<MaterialProperty>.ReadOnly Properties;

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Key.GetHashCode();
                    for (int i = 0; i < Properties.Length; i++)
                    {
                        hash = hash * 31 + Properties.Ptr[i].Name;
                    }
                    return hash;
                }
            }

            public bool Equals(KeyWithProperty other)
            {
                if (!Key.Equals(other.Key))
                {
                    return false;
                }
                if (Properties.Length != other.Properties.Length)
                {
                    return false;
                }
                for (int i = 0; i < Properties.Length; i++)
                {
                    if (Properties.Ptr[i].Name != other.Properties.Ptr[i].Name)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        private readonly Dictionary<KeyWithProperty, int> m_Cache = new();

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
            KeyWithProperty key = new()
            {
                Key = Sorter.Key(mm, entity),
                Properties = Properties
            };
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
            batch.LocalToWorlds.Add(world.Value);
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