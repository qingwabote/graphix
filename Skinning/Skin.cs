using System;
using Bastard;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Mathematics;
using UnityEngine;

namespace Graphix
{
    public struct JointMeta
    {
        public BlobArray<float4x4> InverseBindMatrices;
        public BlobArray<int> Locations;
    }

    public class Skin : ScriptableObject, ISerializationCallbackReceiver
    {
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

        private Store m_Persistent;
        public Store Persistent
        {
            get
            {
                return m_Persistent ??= new Store(Nodes.Length);
            }
        }

        private TransientStore m_Transient;
        public Store Transient
        {
            get
            {
                return m_Transient ??= new TransientStore(Nodes.Length);
            }
        }

        // [HideInInspector]
        public string[] Nodes;

        [SerializeField, HideInInspector]
        private byte[] m_JointMeta;

        [NonSerialized]
        public BlobAssetReference<JointMeta> JointMeta;

        public void OnBeforeSerialize()
        {
            if (this == null) { return; }

            var writer = new MemoryBinaryWriter();
            BlobAssetSerializeExtensions.Write(writer, JointMeta);
            var bytes = new byte[writer.Length];
            unsafe
            {
                fixed (byte* dst = bytes)
                {
                    UnsafeUtility.MemCpy(dst, writer.Data, writer.Length);
                }
            }
            m_JointMeta = bytes;
            writer.Dispose();
        }

        public void OnAfterDeserialize()
        {
            if (this == null) { return; }

            unsafe
            {
                fixed (byte* src = m_JointMeta)
                {
                    var reader = new MemoryBinaryReader(src, m_JointMeta.Length);
                    JointMeta = BlobAssetSerializeExtensions.Read<JointMeta>(reader);
                    reader.Dispose();
                }
            }
            m_JointMeta = null;
        }
    }
}