using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Rendering;
using static Unity.Collections.AllocatorManager;

namespace Graphix
{
    public struct BatcherImpl
    {
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

        internal struct BatchState
        {
            internal int Index;
            internal int Capacity;
        }

        public readonly unsafe ref struct Scope
        {
            private readonly Bastard.UnsafeHashMap<BatchKey, BatchState>* m_States;

            internal Scope(ref Bastard.UnsafeHashMap<BatchKey, BatchState> states)
            {
                m_States = (Bastard.UnsafeHashMap<BatchKey, BatchState>*)UnsafeUtility.AddressOf(ref states);
            }

            public void Merge(ref UnsafeList<Batch> queue, int materialMeshArray, MaterialMeshInfo mm, MaterialPropertyData mp, in float4x4 world, int hashCode = 0)
            {
                if (mm.Material == 0 || mm.Mesh == 0)
                {
                    return;
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
                    state.Index = queue.Length;

                    var b = new Batch(state.Capacity, mm.Material, mm.Mesh, Allocator.Temp);
                    queue.Add(b);
                }

                ref var batch = ref queue.ElementAt(state.Index);
                unsafe
                {
                    foreach (var (name, size, data) in mp)
                    {
                        batch.PropertyDataAdd(name, (byte*)data, size, state.Capacity);
                    }
                }
                batch.LocalToWorlds.Add(world);
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

        public BatcherImpl(AllocatorHandle allocator)
        {
            m_States = new(128, allocator);
        }

        public Scope MakeScope()
        {
            return new Scope(ref m_States);
        }
    }
}
