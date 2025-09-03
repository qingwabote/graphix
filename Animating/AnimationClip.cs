using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;

namespace Graphix
{
    public class AnimationClip : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector]
        private byte[] m_Blob;

        [NonSerialized]
        public BlobAssetReference<Clip> Blob;

        [HideInInspector]
        public string[] Nodes;

        public void OnBeforeSerialize()
        {
            // Debug.Log($"AnimationClip OnBeforeSerialize");
            // FIXME
            if (this == null)
            {
                return;
            }
            var writer = new MemoryBinaryWriter();
            BlobAssetSerializeExtensions.Write(writer, Blob);
            var bytes = new byte[writer.Length];
            unsafe
            {
                fixed (byte* dst = bytes)
                {
                    UnsafeUtility.MemCpy(dst, writer.Data, writer.Length);
                }

            }
            m_Blob = bytes;
            writer.Dispose();
            // Debug.Log($"AnimationClip OnBeforeSerialize done");
        }

        public void OnAfterDeserialize()
        {
            // Debug.Log("AnimationClip OnAfterDeserialize");
            // FIXME
            if (this == null)
            {
                return;
            }
            // Debug.Log($"AnimationClip OnAfterDeserialize _clip_bytes.Length {_clip_bytes.Length}");
            unsafe
            {
                fixed (byte* src = m_Blob)
                {
                    var reader = new MemoryBinaryReader(src, m_Blob.Length);
                    Blob = BlobAssetSerializeExtensions.Read<Clip>(reader);
                    reader.Dispose();
                }

            }
            m_Blob = null;
        }
    }
}