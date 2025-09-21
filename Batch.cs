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