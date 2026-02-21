using System;
using Bastard;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Rendering;
using static Unity.Collections.AllocatorManager;

namespace Graphix
{
    public interface IBatchProgram<TKey>
    {
        TKey GetBatchKey();
        void OnBatchCreated(Batch batch);
    }

    public readonly struct DefaultBatchKey : IEquatable<DefaultBatchKey>
    {
        public override int GetHashCode() { return 0; }
        public bool Equals(DefaultBatchKey other) { return true; }
    }

    public readonly struct DefaultBatchProgram : IBatchProgram<DefaultBatchKey>
    {
        public DefaultBatchKey GetBatchKey() { return default; }
        public void OnBatchCreated(Batch batch) { }
    }

    public struct BatcherImpl<TKey, TProgram> where TKey : unmanaged, IEquatable<TKey> where TProgram : IBatchProgram<TKey>
    {
        internal readonly struct FullKey : IEquatable<FullKey>
        {
            internal readonly int Material;

            internal readonly int Mesh;

            internal readonly int MaterialMeshArray;

            internal readonly TKey Key;

            internal FullKey(int material, int mesh, int materialMeshArray, TKey key)
            {
                Material = material;
                Mesh = mesh;
                MaterialMeshArray = materialMeshArray;
                Key = key;
            }

            public override int GetHashCode()
            {
                return Bastard.HashCode.Combine(Material, Mesh, MaterialMeshArray, Key.GetHashCode());
            }

            public bool Equals(FullKey other)
            {
                return Material == other.Material && Mesh == other.Mesh && MaterialMeshArray == other.MaterialMeshArray && Key.Equals(other.Key);
            }
        }

        internal struct State
        {
            public int Index;
            public int MaxCount;
        }

        public readonly unsafe ref struct Scope
        {
            private readonly Bastard.UnsafeHashMap<FullKey, State>* m_States;

            internal Scope(ref Bastard.UnsafeHashMap<FullKey, State> states)
            {
                m_States = (Bastard.UnsafeHashMap<FullKey, State>*)UnsafeUtility.AddressOf(ref states);
            }

            public void Merge(RecycleQueue<Batch> queue, int materialMeshArray, MaterialMeshInfo mm, MaterialPropertyData mp, in float4x4 world, TProgram program = default)
            {
                Batch batch;
                var key = new FullKey(mm.Material, mm.Mesh, materialMeshArray, program.GetBatchKey());
                ref var state = ref m_States->EnsureValueRef(key, out var uninitialized);
                if (uninitialized)
                {
                    state.Index = -1;
                    state.MaxCount = 0;
                }

                if (state.Index != -1)
                {
                    batch = queue.Data[state.Index];
                }
                else
                {
                    state.Index = queue.Count;

                    batch = queue.Push();
                    batch.Material = mm.Material;
                    batch.Mesh = mm.Mesh;
                    batch.InitialCapacity = math.max(state.MaxCount, 1);
                    program.OnBatchCreated(batch);
                }

                unsafe
                {
                    foreach (var (name, size, data) in mp)
                    {
                        batch.PropertyDataAdd(name, (byte*)data, size);
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

                    var queue = EntitiesGraphicsSystem.GetQueue(kv.Key.MaterialMeshArray);
                    var batch = queue.Data[state.Index];
                    state.MaxCount = math.max(batch.Count, state.MaxCount);
                    state.Index = -1;
                }
            }
        }

        private Bastard.UnsafeHashMap<FullKey, State> m_States;

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