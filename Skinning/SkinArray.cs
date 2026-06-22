using System;
using Bastard;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

namespace Graphix
{
    public struct SkinArray : ISharedComponentData, IEquatable<SkinArray>
    {
        internal class RuntimeData
        {
            private Store[] m_Persistent;
            private TransientStore[] m_Transient;

            public Store GetCurrentStore(Skin[] skins, SkinInfo info)
            {
                return info.Baking ? GetPersistent(skins, info.Skin) : GetTransient(skins, info.Skin);
            }

            private Store GetPersistent(Skin[] skins, int index)
            {
                m_Persistent ??= new Store[skins.Length];
                return m_Persistent[index] ??= new Store(skins[index].Nodes.Length);
            }

            private Store GetTransient(Skin[] skins, int index)
            {
                m_Transient ??= new TransientStore[skins.Length];
                return m_Transient[index] ??= new TransientStore(skins[index].Nodes.Length);
            }
        }

        public unsafe class Store
        {
            protected readonly TextureView m_View;
            public Texture2D Texture => m_View.Texture;
            public NativeArray<float>* Source => m_View.Source.GetUnsafePtr();

            private readonly int m_Stride;

            public Store(int stride)
            {
                m_Stride = stride;
                m_View = new TextureView();
            }

            virtual public int Add()
            {
                return m_View.AddBlock(12 * m_Stride);
            }

            public void Update()
            {
                m_View.Update();
            }
        }

        public class TransientStore : Store
        {
            private Transient<int> m_reset = new(0, 0);

            public TransientStore(int stride) : base(stride) { }

            override public int Add()
            {
                if (m_reset.Value == 0)
                {
                    m_View.Reset();
                    m_reset.Value = 1;
                }
                return base.Add();
            }
        }

        public Skin[] Data;

        [SerializeField]
        private RuntimeData m_RuntimeData;

        public uint4 Hash128;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Store GetCurrentStore(SkinInfo info)
        {
            return m_RuntimeData.GetCurrentStore(Data, info);
        }

        public override int GetHashCode()
        {
            return (int)Hash128.x;
        }

        public bool Equals(SkinArray other)
        {
            return math.all(Hash128 == other.Hash128);
        }

#if UNITY_EDITOR
        internal SkinArray(Skin[] data)
        {
            Data = data;
            m_RuntimeData = new RuntimeData();

            Hash128 = uint4.zero;
            Hash128 = ComputeHash128();
        }

        private uint4 ComputeHash128()
        {
            var hash = new xxHash3.StreamingState(false);

            hash.Update(Data.Length);

            for (int i = 0; i < Data.Length; ++i)
                AssetHash.Update(ref hash, Data[i]);

            uint4 H = hash.DigestHash128();

            // Make sure the hash is never exactly zero, to keep zero as a null value
            if (math.all(H == uint4.zero))
                return new uint4(1, 0, 0, 0);

            return H;
        }
#endif
    }
}
