using System;
using System.Runtime.CompilerServices;
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
    public struct Batcher
    {
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
            public unsafe ref struct ChunkBatcher
            {
                private readonly Bastard.UnsafeHashMap<BatchKey, BatchState>* m_States;

                private readonly UnsafeList<Batch>* m_Queue;

                private readonly UnsafeList<MaterialProperty>.ReadOnly m_Properties;

                private ArchetypeChunk m_Chunk;

                private ComponentTypeHandle<LocalToWorld> m_LocalToWorld;

                private const int ChunkBatchCapacity = 32;
                private const int ChunkElementCapacity = 128;

                private fixed int m_BatchSet[ChunkBatchCapacity];
                private fixed int m_BatchToProperty[ChunkBatchCapacity];
                private int m_BatchCount;

                private fixed int m_ElementToBatch[ChunkElementCapacity];
                private fixed int m_ElementToEntity[ChunkElementCapacity];
                private int m_ElementCount;

                internal ChunkBatcher(Bastard.UnsafeHashMap<BatchKey, BatchState>* states, UnsafeList<Batch>* queue, UnsafeList<MaterialProperty>.ReadOnly properties, in ArchetypeChunk chunk, ref ComponentTypeHandle<LocalToWorld> localToWorld)
                {
                    m_States = states;
                    m_Queue = queue;
                    m_Properties = properties;
                    m_Chunk = chunk;
                    m_LocalToWorld = localToWorld;
                    m_ElementCount = 0;
                    m_BatchCount = 0;
                }

                public int Add(int materialMeshArray, MaterialMeshInfo mm, int entity, int element = 0, int hashCode = 0)
                {
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
                        if (m_BatchSet[i] == batchIndex)
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
                        m_BatchSet[m_BatchCount++] = batchIndex;
                    }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    if (m_ElementCount >= ChunkElementCapacity)
                    {
                        throw new InvalidOperationException($"ChunkScope element capacity exceeded: {ChunkElementCapacity}.");
                    }
#endif
                    m_ElementToBatch[m_ElementCount] = batchSlot;
                    m_ElementToEntity[m_ElementCount] = entity;
                    m_ElementCount++;

                    return batchIndex;
                }

                public void Dispose()
                {
                    for (int i = 0; i < m_Properties.Length; i++)
                    {
                        var property = m_Properties.Ptr[i];
                        for (int j = 0; j < m_BatchCount; j++)
                        {
                            var batchIndex = m_BatchSet[j];
                            ref var batch = ref m_Queue->ElementAt(batchIndex);
                            m_BatchToProperty[j] = batch.PropertyDataEnsure(property.Name, property.TypeSize, batch.LocalToWorlds.Capacity);
                        }

                        ref var handle = ref MaterialProperty.Handles.Data.ElementAt(property.TypeIndex);

                        if (property.TypeIsBuffer)
                        {
                            var bufferAccessor = m_Chunk.GetUntypedBufferAccessor(ref handle);
                            var elementIndex = 0;
                            while (elementIndex < m_ElementCount)
                            {
                                var entity = m_ElementToEntity[elementIndex];
                                var ptr = (byte*)bufferAccessor.GetUnsafeReadOnlyPtr(entity);
                                var element = 0;
                                do
                                {
                                    var batchSlot = m_ElementToBatch[elementIndex];
                                    ref var batch = ref m_Queue->ElementAt(m_BatchSet[batchSlot]);
                                    batch.PropertyDataAdd(m_BatchToProperty[batchSlot], ptr + element * property.TypeSize, property.TypeSize);
                                    elementIndex++;
                                    element++;
                                }
                                while (elementIndex < m_ElementCount && m_ElementToEntity[elementIndex] == entity);
                            }
                        }
                        else
                        {
                            var ptr = (byte*)m_Chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref handle, property.TypeSize).GetUnsafeReadOnlyPtr();
                            for (int elementIndex = 0; elementIndex < m_ElementCount; elementIndex++)
                            {
                                var batchSlot = m_ElementToBatch[elementIndex];
                                ref var batch = ref m_Queue->ElementAt(m_BatchSet[batchSlot]);
                                batch.PropertyDataAdd(m_BatchToProperty[batchSlot], ptr + m_ElementToEntity[elementIndex] * property.TypeSize, property.TypeSize);
                            }
                        }
                    }

                    var localToWorlds = (LocalToWorld*)m_Chunk.GetNativeArray(ref m_LocalToWorld).GetUnsafeReadOnlyPtr();
                    for (int i = 0; i < m_ElementCount; i++)
                    {
                        ref var batch = ref m_Queue->ElementAt(m_BatchSet[m_ElementToBatch[i]]);
                        batch.LocalToWorlds.Add(localToWorlds[m_ElementToEntity[i]].Value);
                    }
                }

            }

            private readonly Bastard.UnsafeHashMap<BatchKey, BatchState>* m_States;

            internal Scope(ref Bastard.UnsafeHashMap<BatchKey, BatchState> states)
            {
                m_States = (Bastard.UnsafeHashMap<BatchKey, BatchState>*)UnsafeUtility.AddressOf(ref states);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ChunkBatcher AutoChunk(ref UnsafeList<Batch> queue, ref SystemState state, in ArchetypeChunk chunk, ref ComponentTypeHandle<LocalToWorld> localToWorld)
            {
                var properties = MaterialProperty.Get(chunk.Archetype);
                for (int i = 0; i < properties.Length; i++)
                {
                    ref var handle = ref MaterialProperty.Handles.Data.ElementAt(properties.Ptr[i].TypeIndex);
                    handle.Update(ref state);
                }

                return new ChunkBatcher(
                    m_States,
                    (UnsafeList<Batch>*)UnsafeUtility.AddressOf(ref queue),
                    properties,
                    chunk,
                    ref localToWorld
                    );
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

        private Bastard.UnsafeHashMap<BatchKey, BatchState> m_States;

        public Batcher(AllocatorHandle allocator)
        {
            m_States = new(128, allocator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Scope Auto()
        {
            return new Scope(ref m_States);
        }
    }
}
