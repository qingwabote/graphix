using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace Graphix
{
    public interface IBatchProgram<TKey>
    {
        TKey KeyGen(int entity, MaterialMeshInfo mm);
        void OnBatch(int entity, Batch batch);
    }

    public struct BatchProgram : IBatchProgram<MaterialMeshInfo>
    {
        public MaterialMeshInfo KeyGen(int entity, MaterialMeshInfo mm)
        {
            return mm;
        }

        public void OnBatch(int entity, Batch batch) { }
    }

    public unsafe ref struct BatcherImpl<TKey, TProgram> where TKey : unmanaged, IEquatable<TKey> where TProgram : IBatchProgram<TKey>
    {
        struct BatchKey : IEquatable<BatchKey>
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

            public bool Equals(BatchKey other)
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

        private NativeHashMap<BatchKey, int> m_Cache;

        private UnsafeList<MaterialProperty>.ReadOnly m_Properties;
        private void** m_PropertyData;

        public BatcherImpl(int initialCapacity)
        {
            m_Cache = new(initialCapacity, Allocator.Temp);

            m_Properties = default;
            m_PropertyData = (void**)UnsafeUtility.Malloc(sizeof(void*) * MaterialProperty.MaxCount, UnsafeUtility.AlignOf<IntPtr>(), Allocator.Temp);
        }

        public void BeginChunk(ref SystemState state, ArchetypeChunk chunk)
        {
            m_Properties = MaterialProperty.Get(chunk.Archetype);
            for (int i = 0; i < m_Properties.Length; i++)
            {
                var property = m_Properties.Ptr[i];
                ref var handle = ref MaterialProperty.Handles[property.Type];
                handle.Update(ref state);
                m_PropertyData[i] = chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref handle, property.Size).GetUnsafeReadOnlyPtr();
            }
        }

        public void Add(int entity, in float4x4 world, MaterialMeshInfo mm)
        {
            TProgram program = default;
            Add(entity, world, mm, ref program);
        }

        public void Add(int entity, in float4x4 world, MaterialMeshInfo mm, ref TProgram program)
        {
            Batch batch;
            BatchKey key = new()
            {
                Key = program.KeyGen(entity, mm),
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
                batch.Material = mm.Material;
                batch.Mesh = mm.Mesh;

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

                program.OnBatch(entity, batch);
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

        public void EndChunk() { }
    }
}