using System;
using Bastard;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using static Unity.Collections.AllocatorManager;

namespace Graphix
{
    public struct BatcherImpl
    {
        private const int ChunkBatchCapacity = 32;
        private const int ChunkElementCapacity = 128;

        internal unsafe struct Chunk
        {
            public fixed int BatchSet[ChunkBatchCapacity];
            public fixed int PropertyMap[ChunkBatchCapacity];
            public fixed int ElementToBatch[ChunkElementCapacity];
            public fixed int ElementToEntity[ChunkElementCapacity];
            public fixed int ElementToIndex[ChunkElementCapacity];
        }

        internal struct BatchState
        {
            internal int Index;
            internal int Capacity;
        }

        internal readonly struct BatchKey : IEquatable<BatchKey>
        {
            internal readonly int Material;

            internal readonly int Mesh;

            internal readonly int MaterialMeshArray;

            internal readonly int Key;

            internal BatchKey(int material, int mesh, int materialMeshArray, int key)
            {
                Material = material;
                Mesh = mesh;
                MaterialMeshArray = materialMeshArray;
                Key = key;
            }

            public override int GetHashCode()
            {
                return Bastard.HashCode.Combine(Material, Mesh, MaterialMeshArray, Key);
            }

            public bool Equals(BatchKey other)
            {
                return Material == other.Material && Mesh == other.Mesh && MaterialMeshArray == other.MaterialMeshArray && Key == other.Key;
            }
        }

        public readonly unsafe ref struct Scope
        {
            private readonly Bastard.UnsafeHashMap<BatchKey, BatchState>* m_States;

            private readonly Chunk* m_Chunk;

            internal Scope(ref Bastard.UnsafeHashMap<BatchKey, BatchState> states, ref Chunk chunk)
            {
                m_States = (Bastard.UnsafeHashMap<BatchKey, BatchState>*)UnsafeUtility.AddressOf(ref states);
                m_Chunk = (Chunk*)UnsafeUtility.AddressOf(ref chunk);
            }

            public ChunkScope MakeChunk(ref UnsafeList<Batch> queue)
            {
                return new ChunkScope(m_States, m_Chunk, (UnsafeList<Batch>*)UnsafeUtility.AddressOf(ref queue));
            }

            public void Dispose()
            {
                foreach (var kv in *m_States)
                {
                    ref var state = ref kv.Value;
                    if (state.Index == -1)
                    {
                        continue;
                    }

                    ref var queue = ref EntitiesGraphicsSystemUnmanaged.GetQueue(kv.Key.MaterialMeshArray);
                    ref var batch = ref queue.ElementAt(state.Index);
                    state.Capacity = math.max(batch.Count, state.Capacity);
                    state.Index = -1;
                }
            }
        }

        public unsafe ref struct ChunkScope
        {
            private readonly Bastard.UnsafeHashMap<BatchKey, BatchState>* m_States;

            private readonly Chunk* m_Chunk;

            private readonly UnsafeList<Batch>* m_Queue;

            private int m_ElementCount;

            private int m_BatchCount;

            internal ChunkScope(Bastard.UnsafeHashMap<BatchKey, BatchState>* states, Chunk* chunk, UnsafeList<Batch>* queue)
            {
                m_States = states;
                m_Chunk = chunk;
                m_Queue = queue;
                m_ElementCount = 0;
                m_BatchCount = 0;
            }

            public int Record(int materialMeshArray, MaterialMeshInfo mm, int entity, int element = 0, int hashCode = 0)
            {
                if (mm.Material == 0 || mm.Mesh == 0)
                {
                    return -1;
                }

                var key = new BatchKey(mm.Material, mm.Mesh, materialMeshArray, hashCode);
                ref var state = ref m_States->EnsureValueRef(key, out var uninitialized);
                if (uninitialized)
                {
                    state.Index = -1;
                    state.Capacity = 1;
                }

                if (state.Index == -1)
                {
                    state.Index = m_Queue->Length;
                    m_Queue->Add(new Batch(state.Capacity, mm.Material, mm.Mesh, Allocator.Temp));
                }

                var batchIndex = state.Index;

                var batchSlot = -1;
                for (int i = 0; i < m_BatchCount; i++)
                {
                    if (m_Chunk->BatchSet[i] == batchIndex)
                    {
                        batchSlot = i;
                        break;
                    }
                }

                if (batchSlot == -1)
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    if (m_BatchCount >= ChunkBatchCapacity)
                    {
                        throw new InvalidOperationException($"ChunkScope batch capacity exceeded: {ChunkBatchCapacity}.");
                    }
#endif
                    batchSlot = m_BatchCount;
                    m_Chunk->BatchSet[m_BatchCount++] = batchIndex;
                }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (m_ElementCount >= ChunkElementCapacity)
                {
                    throw new InvalidOperationException($"ChunkScope element capacity exceeded: {ChunkElementCapacity}.");
                }
#endif
                m_Chunk->ElementToBatch[m_ElementCount] = batchSlot;
                m_Chunk->ElementToEntity[m_ElementCount] = entity;
                m_Chunk->ElementToIndex[m_ElementCount] = element;
                m_ElementCount++;

                return batchIndex;
            }

            public void Flush(ref SystemState state, in ArchetypeChunk chunk, ref ComponentTypeHandle<LocalToWorld> LocalToWorld)
            {
                var properties = MaterialProperty.Get(chunk.Archetype);
                for (int i = 0; i < properties.Length; i++)
                {
                    var property = properties.Ptr[i];
                    for (int j = 0; j < m_BatchCount; j++)
                    {
                        var batchIndex = m_Chunk->BatchSet[j];
                        ref var batch = ref m_Queue->ElementAt(batchIndex);
                        m_Chunk->PropertyMap[j] = batch.PropertyDataEnsure(property.Name, property.TypeSize, batch.LocalToWorlds.Capacity);
                    }

                    ref var handle = ref MaterialProperty.Handles.Data.ElementAt(property.TypeIndex);
                    handle.Update(ref state);

                    if (property.TypeIsBuffer)
                    {
                        var bufferAccessor = chunk.GetUntypedBufferAccessor(ref handle);
                        var elementIndex = 0;
                        while (elementIndex < m_ElementCount)
                        {
                            var entity = m_Chunk->ElementToEntity[elementIndex];
                            var ptr = (byte*)bufferAccessor.GetUnsafeReadOnlyPtr(entity);

                            do
                            {
                                var batchSlot = m_Chunk->ElementToBatch[elementIndex];
                                ref var batch = ref m_Queue->ElementAt(m_Chunk->BatchSet[batchSlot]);
                                batch.PropertyDataAdd(m_Chunk->PropertyMap[batchSlot], ptr + m_Chunk->ElementToIndex[elementIndex] * property.TypeSize, property.TypeSize);
                                elementIndex++;
                            }
                            while (elementIndex < m_ElementCount && m_Chunk->ElementToEntity[elementIndex] == entity);
                        }
                    }
                    else
                    {
                        var ptr = (byte*)chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref handle, property.TypeSize).GetUnsafeReadOnlyPtr();
                        for (int elementIndex = 0; elementIndex < m_ElementCount; elementIndex++)
                        {
                            var batchSlot = m_Chunk->ElementToBatch[elementIndex];
                            ref var batch = ref m_Queue->ElementAt(m_Chunk->BatchSet[batchSlot]);
                            batch.PropertyDataAdd(m_Chunk->PropertyMap[batchSlot], ptr + m_Chunk->ElementToEntity[elementIndex] * property.TypeSize, property.TypeSize);
                        }
                    }
                }

                var localToWorlds = (LocalToWorld*)chunk.GetNativeArray(ref LocalToWorld).GetUnsafeReadOnlyPtr();
                for (int i = 0; i < m_ElementCount; i++)
                {
                    ref var batch = ref m_Queue->ElementAt(m_Chunk->BatchSet[m_Chunk->ElementToBatch[i]]);
                    batch.LocalToWorlds.Add(localToWorlds[m_Chunk->ElementToEntity[i]].Value);
                }
            }

        }

        private Bastard.UnsafeHashMap<BatchKey, BatchState> m_States;

        private Chunk m_Chunk;

        public BatcherImpl(AllocatorHandle allocator)
        {
            m_States = new(128, allocator);
            m_Chunk = default;
        }

        public Scope MakeScope()
        {
            return new Scope(ref m_States, ref m_Chunk);
        }
    }
}
