using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace Graphix
{
    public struct NoParam { }

    public interface IBatchSorter<TKey, TParam>
    {
        TKey KeyGen(MaterialMeshInfo mm, TParam param);
        void BatchInit(Batch batch, MaterialMeshInfo mm, TParam param);
    }

    public struct BatchSorter : IBatchSorter<MaterialMeshInfo, NoParam>
    {
        public MaterialMeshInfo KeyGen(MaterialMeshInfo mm, NoParam param)
        {
            return mm;
        }

        public void BatchInit(Batch batch, MaterialMeshInfo mm, NoParam param)
        {
            batch.Material = mm.Material;
            batch.Mesh = mm.Mesh;
        }
    }

    public unsafe class BatcherImpl<TKey, TSorter, TParam> where TKey : unmanaged, IEquatable<TKey> where TSorter : IBatchSorter<TKey, TParam>
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

        private UnsafeList<MaterialProperty>.ReadOnly m_Properties;
        private void** m_PropertyData = null;

        public void BeginChunk(ref SystemState state, ArchetypeChunk chunk)
        {
            m_Properties = MaterialProperty.Get(chunk.Archetype);
            m_PropertyData = (void**)UnsafeUtility.Malloc(sizeof(void*) * m_Properties.Length, UnsafeUtility.AlignOf<IntPtr>(), Allocator.Temp);
            for (int i = 0; i < m_Properties.Length; i++)
            {
                var property = m_Properties.Ptr[i];
                ref var handle = ref MaterialProperty.Handles[property.Type];
                handle.Update(ref state);
                m_PropertyData[i] = chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref handle, property.Size).GetUnsafeReadOnlyPtr();
            }
        }

        public void Add(int entity, in float4x4 world, MaterialMeshInfo mm, TParam param = default)
        {
            Batch batch;
            KeyWithProperty key = new()
            {
                Key = Sorter.KeyGen(mm, param),
                Properties = m_Properties
            };
            if (m_Cache.TryGetValue(key, out int index))
            {
                batch = EntitiesGraphicsSystem.Queue.Data[index];
            }
            else
            {
                m_Cache.Add(key, EntitiesGraphicsSystem.Queue.Count);
                batch = EntitiesGraphicsSystem.Queue.Push();
                Sorter.BatchInit(batch, mm, param);

                for (int i = 0; i < m_Properties.Length; i++)
                {
                    var property = m_Properties.Ptr[i];
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

            for (int i = 0; i < m_Properties.Length; i++)
            {
                var property = m_Properties.Ptr[i];
                if (property.Size == sizeof(float))
                {
                    var data = (float*)m_PropertyData[i];
                    batch.PropertyFloatAdd(property.Name, data[entity]);
                }
                else
                {
                    var data = (float4*)m_PropertyData[i];
                    batch.PropertyVectorAdd(property.Name, data[entity]);
                }
            }
            batch.LocalToWorlds.Add(world);
        }

        public void EndChunk()
        {
            m_Properties = default;
            m_PropertyData = null;
        }

        public void Clear()
        {
            m_Cache.Clear();
        }
    }
}