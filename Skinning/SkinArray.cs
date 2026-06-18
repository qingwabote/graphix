using System;
using Bastard;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

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

        static private List<SkinArray> s_Instances = new();

        static public SkinArray GetCurrent(EntityManager entityManager)
        {
            s_Instances.Clear();
            entityManager.GetAllUniqueSharedComponentsManaged(s_Instances);
            return s_Instances[1];
        }

        public Skin[] Data;

        [SerializeField]
        private int m_HashCode;

        [SerializeField]
        private RuntimeData m_RuntimeData;

        internal SkinArray(Skin[] data, int hashCode)
        {
            Data = data;
            m_HashCode = hashCode;

            m_RuntimeData = new RuntimeData();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Store GetCurrentStore(SkinInfo info)
        {
            return m_RuntimeData.GetCurrentStore(Data, info);
        }

        public override int GetHashCode()
        {
            return m_HashCode;
        }

        public bool Equals(SkinArray other)
        {
            return m_HashCode == other.m_HashCode;
        }
    }
}
