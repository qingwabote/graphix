using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Rendering;
using static Unity.Collections.AllocatorManager;

namespace Graphix
{
    public interface IBatchProgram<TKey>
    {
        TKey GetBatchKey();
        void OnBatchCreated(ref Batch batch);
    }

    public readonly struct DefaultBatchKey : IEquatable<DefaultBatchKey>
    {
        public override int GetHashCode() { return 0; }
        public bool Equals(DefaultBatchKey other) { return true; }
    }

    public readonly struct DefaultBatchProgram : IBatchProgram<DefaultBatchKey>
    {
        public DefaultBatchKey GetBatchKey() { return default; }
        public void OnBatchCreated(ref Batch batch) { }
    }

    public struct BatcherImpl<TKey, TProgram> where TKey : unmanaged, IEquatable<TKey> where TProgram : unmanaged, IBatchProgram<TKey>
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

        public readonly unsafe ref struct Scope
        {
            private readonly Bastard.UnsafeHashMap<FullKey, Batch.State>* m_States;

            internal Scope(ref Bastard.UnsafeHashMap<FullKey, Batch.State> states)
            {
                m_States = (Bastard.UnsafeHashMap<FullKey, Batch.State>*)UnsafeUtility.AddressOf(ref states);
            }

            public void Merge(ref UnsafeList<Batch> queue, int materialMeshArray, MaterialMeshInfo mm, MaterialPropertyData mp, in float4x4 world, TProgram program = default)
            {
                var key = new FullKey(mm.Material, mm.Mesh, materialMeshArray, program.GetBatchKey());
                ref var state = ref m_States->EnsureValueRef(key, out var uninitialized);
                if (uninitialized)
                {
                    state.Index = -1;
                    state.MaxCount = 1;
                }

                if (state.Index == -1)
                {
                    state.Index = queue.Length;

                    var b = new Batch(ref state, mm.Material, mm.Mesh, Allocator.Temp);
                    program.OnBatchCreated(ref b);
                    queue.Add(b);
                }

                ref var batch = ref queue.ElementAt(state.Index);
                unsafe
                {
                    foreach (var (name, size, data) in mp)
                    {
                        batch.PropertyDataAdd(ref state, name, (byte*)data, size);
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
                    state.MaxCount = math.max(batch.Count, state.MaxCount);
                    state.Index = -1;
                }
            }
        }

        private Bastard.UnsafeHashMap<FullKey, Batch.State> m_States;

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