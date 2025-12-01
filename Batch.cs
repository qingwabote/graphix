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

        public NativeList<float4x4> LocalToWorlds = new(Allocator.Persistent);
        public int Count => LocalToWorlds.Length;

        private readonly Dictionary<int, Texture> m_Textures = new();
        private readonly Dictionary<int, List<float>> m_Floats = new();
        private readonly Dictionary<int, List<Vector4>> m_Vectors = new();

        public bool PropertyAcquired => m_Floats.Count > 0 || m_Vectors.Count > 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PropertyTextureBind(int name, Texture texture)
        {
            m_Textures.Add(name, texture);
        }

        public void PropertyFloatAdd(int name, float value)
        {
            if (!m_Floats.TryGetValue(name, out var list))
            {
                list = s_FloatPool.Get();
                list.Clear();
                m_Floats.Add(name, list);
            }
            for (int i = list.Count; i < Count; i++)
            {
                list.Add(default);
            }
            list.Add(value);
        }

        public void PropertyVectorAdd(int name, Vector4 value)
        {
            if (!m_Vectors.TryGetValue(name, out var list))
            {
                list = s_VectorPool.Get();
                list.Clear();
                m_Vectors.Add(name, list);
            }
            for (int i = list.Count; i < Count; i++)
            {
                list.Add(default);
            }
            list.Add(value);
        }

        public void PropertyToBlock(MaterialPropertyBlock output)
        {
            foreach (var (id, texture) in m_Textures)
            {
                output.SetTexture(id, texture);
            }

            foreach (var (id, list) in m_Floats)
            {
                output.SetFloatArray(id, list);
            }

            foreach (var (id, list) in m_Vectors)
            {
                output.SetVectorArray(id, list);
            }
        }

        public void PropertyToBlock(int index, MaterialPropertyBlock output)
        {
            foreach (var (id, list) in m_Floats)
            {
                output.SetFloat(id, list[index]);
            }

            foreach (var (id, list) in m_Vectors)
            {
                output.SetVector(id, list[index]);
            }
        }

        public void Clear()
        {
            LocalToWorlds.Clear();

            m_Textures.Clear();
            m_Floats.Clear();
            m_Vectors.Clear();
        }
    }
}