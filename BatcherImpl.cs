using System;
using Bastard;
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
        struct KeyWithMaterialMeshArray : IEquatable<KeyWithMaterialMeshArray>
        {
            internal TKey Key;

            internal int MaterialMeshArray;

            public override int GetHashCode()
            {
                return Bastard.HashCode.Combine(Key.GetHashCode(), MaterialMeshArray);
            }

            public bool Equals(KeyWithMaterialMeshArray other)
            {
                return Key.Equals(other.Key) && MaterialMeshArray == other.MaterialMeshArray;
            }
        }

        private SharedComponentTypeHandle<MaterialMeshArray> m_MaterialMeshArray;

        private int m_MaterialMeshArrayIndex;
        private NativeHashMap<KeyWithMaterialMeshArray, int> m_Cache;
        private RecycleQueue<Batch> m_Queue;

        private UnsafeList<MaterialProperty>.ReadOnly m_Properties;
        private void** m_PropertyData;

        public BatcherImpl(SharedComponentTypeHandle<MaterialMeshArray> MaterialMeshArray, int initialCapacity)
        {
            m_MaterialMeshArray = MaterialMeshArray;
            m_MaterialMeshArrayIndex = 0;
            m_Cache = new(initialCapacity, Allocator.Temp);
            m_Queue = null;

            m_Properties = default;
            m_PropertyData = (void**)UnsafeUtility.Malloc(sizeof(void*) * MaterialProperty.MaxCount, UnsafeUtility.AlignOf<IntPtr>(), Allocator.Temp);
        }

        public void BeginChunk(ref SystemState state, ArchetypeChunk chunk)
        {
            m_MaterialMeshArrayIndex = chunk.GetSharedComponentIndex(m_MaterialMeshArray);
            m_Queue = EntitiesGraphicsSystem.GetQueue(m_MaterialMeshArrayIndex);

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
            var key = new KeyWithMaterialMeshArray
            {
                Key = program.KeyGen(entity, mm),
                MaterialMeshArray = m_MaterialMeshArrayIndex
            };
            if (m_Cache.TryGetValue(key, out int index))
            {
                batch = m_Queue.Data[index];
            }
            else
            {
                m_Cache.Add(key, m_Queue.Count);
                batch = m_Queue.Push();
                batch.Material = mm.Material;
                batch.Mesh = mm.Mesh;
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