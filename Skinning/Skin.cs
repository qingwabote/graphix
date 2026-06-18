using System;
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
