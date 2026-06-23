using System;
using Bastard;
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
            private readonly struct ClipFrame : IEquatable<ClipFrame>
            {
                private readonly int m_Skin;
                private readonly BlobAssetReference<Clip> m_Clip;
                private readonly int m_Frame;

                public ClipFrame(int skin, BlobAssetReference<Clip> clip, int frame)
                {
                    m_Skin = skin;
                    m_Clip = clip;
                    m_Frame = frame;
                }

                public override int GetHashCode()
                {
                    return Bastard.HashCode.Combine(m_Skin, m_Clip.GetHashCode(), m_Frame);
                }

                public bool Equals(ClipFrame other)
                {
                    return m_Skin == other.m_Skin && m_Clip == other.m_Clip && m_Frame == other.m_Frame;
                }
            }

            private Store[] m_Persistent;
            private TransientStore[] m_Transient;
            private NativeHashMap<ClipFrame, int> m_Offsets;

            public Store GetCurrentStore(Skin[] skins, SkinInfo info)
            {
                return info.Baking ? GetPersistent(skins, info.Skin) : GetTransient(skins, info.Skin);
            }

            public bool GetOffset(SkinInfo info, BlobAssetReference<Clip> clip, int frame, out int offset)
            {
                if (!m_Offsets.IsCreated)
                {
                    offset = default;
                    return false;
                }

                return m_Offsets.TryGetValue(new ClipFrame(info.Skin, clip, frame), out offset);
            }

            public void SetOffset(SkinInfo info, BlobAssetReference<Clip> clip, int frame, int offset)
            {
                if (!m_Offsets.IsCreated)
                {
                    m_Offsets = new NativeHashMap<ClipFrame, int>(1024, Allocator.Persistent);
                }

                m_Offsets.Add(new ClipFrame(info.Skin, clip, frame), offset);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetOffset(SkinInfo info, BlobAssetReference<Clip> clip, int frame, out int offset)
        {
            return m_RuntimeData.GetOffset(info, clip, frame, out offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetOffset(SkinInfo info, BlobAssetReference<Clip> clip, int frame, int offset)
        {
            m_RuntimeData.SetOffset(info, clip, frame, offset);
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
