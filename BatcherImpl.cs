using System;
using Bastard;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Rendering;

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

    public unsafe ref struct BatcherImpl<TKey, TProgram> where TKey : unmanaged, IEquatable<TKey> where TProgram : IBatchProgram<TKey>
    {
        internal readonly struct FullBatchKey : IEquatable<FullBatchKey>
        {
            internal readonly int Material;

            internal readonly int Mesh;

            internal readonly int MaterialMeshArray;

            internal readonly TKey Key;

            internal FullBatchKey(int material, int mesh, int materialMeshArray, TKey key)
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

            public bool Equals(FullBatchKey other)
            {
                return Material == other.Material && Mesh == other.Mesh && MaterialMeshArray == other.MaterialMeshArray && Key.Equals(other.Key);
            }
        }

        private NativeHashMap<FullBatchKey, int> m_Cache;

        public BatcherImpl(int initialCapacity)
        {
            m_Cache = new(initialCapacity, Allocator.Temp);
        }

        public void Add(RecycleQueue<Batch> queue, int materialMeshArray, MaterialMeshInfo mm, MaterialPropertyData mp, in float4x4 world, TProgram program = default)
        {
            Batch batch;
            var key = new FullBatchKey(mm.Material, mm.Mesh, materialMeshArray, program.GetBatchKey());
            if (m_Cache.TryGetValue(key, out int index))
            {
                batch = queue.Data[index];
            }
            else
            {
                m_Cache.Add(key, queue.Count);
                batch = queue.Push();
                batch.Material = mm.Material;
                batch.Mesh = mm.Mesh;
                program.OnBatchCreated(batch);
            }

            foreach (var (name, size, data) in mp)
            {
                batch.PropertyDataAdd(name, (byte*)data, size);
            }

            batch.LocalToWorlds.Add(world);
        }
    }
}