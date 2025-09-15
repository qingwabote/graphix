using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Bastard;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Graphix
{
    public class Batch
    {
        private static readonly Dictionary<int, int> s_Cache = new();
        private static readonly RecycleQueue<Batch> s_Queue = new();
        private static readonly MaterialPropertyBlock s_MPB = new();

        private static readonly int s_CountEntry = Profile.DefineEntry("Count");
        private static readonly int s_DrawEntry = Profile.DefineEntry("Draw");

        public static bool Get(int key, out Batch batch)
        {
            if (s_Cache.TryGetValue(key, out int index))
            {
                batch = s_Queue.Data[index];
                return false;
            }

            s_Cache.Add(key, s_Queue.Count);
            batch = s_Queue.Push();
            return true;
        }

        public static void Render(Material[] materials, Mesh[] meshes)
        {
            Profile.Set(s_CountEntry, s_Queue.Count);

            using (new Profile.Scope(s_DrawEntry))
            {
                foreach (var batch in s_Queue.Drain())
                {
                    s_MPB.Clear();
                    batch.PropertyDrain(s_MPB);

                    var rp = new RenderParams(materials[batch.Material])
                    {
                        matProps = s_MPB
                    };
                    Graphics.RenderMeshInstanced(rp, meshes[batch.Mesh], 0, batch.Worlds.AsArray().Reinterpret<Matrix4x4>(), batch.Count);

                    batch.Worlds.Clear();
                    batch.Count = 0;
                }
                s_Cache.Clear();
            }
        }


        private static readonly TransientPool<List<float>> s_FloatPool = new();
        private static readonly TransientPool<List<Vector4>> s_VectorPool = new();

        public int Mesh;
        public int Material;

        public NativeList<float4x4> Worlds = new(Allocator.Persistent);
        public int Count;

        private readonly Dictionary<int, Texture> m_Textures = new();
        private readonly Dictionary<int, List<float>> m_Floats = new();
        private readonly Dictionary<int, List<Vector4>> m_Vectors = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PropertyTextureBind(int name, Texture texture)
        {
            m_Textures.Add(name, texture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PropertyFloatAcquire(int name)
        {
            var list = s_FloatPool.Get();
            list.Clear();
            m_Floats.Add(name, list);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PropertyFloatAdd(int name, float value)
        {
            m_Floats.TryGetValue(name, out var list);
            list.Add(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PropertyVectorAcquire(int name)
        {
            var list = s_VectorPool.Get();
            list.Clear();
            m_Vectors.Add(name, list);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PropertyVectorAdd(int name, Vector4 value)
        {
            m_Vectors.TryGetValue(name, out var list);
            list.Add(value);
        }

        public void PropertyDrain(MaterialPropertyBlock output)
        {
            foreach (var (id, texture) in m_Textures)
            {
                output.SetTexture(id, texture);
            }
            m_Textures.Clear();

            foreach (var (id, list) in m_Floats)
            {
                output.SetFloatArray(id, list);
            }
            m_Floats.Clear();

            foreach (var (id, list) in m_Vectors)
            {
                output.SetVectorArray(id, list);
            }
            m_Vectors.Clear();
        }
    }
}