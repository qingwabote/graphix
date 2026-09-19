#if UNITY_EDITOR
using System;
#endif
using Bastard;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Graphix
{
    public static class PoseCache
    {
        public class Entry
        {
            struct Clip
            {
                public ulong Hash;
                public NativeArray<int> Frame2Offset;
            }

            readonly Store m_Persistent;
            readonly TransientStore m_Transient;
            UnsafeList<Clip> m_Clips;

            internal Entry(int stride)
            {
                m_Persistent = new Store(stride);
                m_Transient = new TransientStore(stride);
                m_Clips = new UnsafeList<Clip>(4, Allocator.Persistent);
            }

            public Store GetStore(bool baking)
            {
                return baking ? m_Persistent : m_Transient;
            }

            public int GetOffset(ulong clipHash, int frame)
            {
                for (int i = 0; i < m_Clips.Length; i++)
                {
                    if (m_Clips[i].Hash == clipHash)
                    {
                        return m_Clips.ElementAt(i).Frame2Offset[frame];
                    }
                }

                return -1;
            }

            public void SetOffset(ulong clipHash, int frame, int count, int offset)
            {
                for (int i = 0; i < m_Clips.Length; i++)
                {
                    if (m_Clips[i].Hash == clipHash)
                    {
                        m_Clips.ElementAt(i).Frame2Offset[frame] = offset;
                        return;
                    }
                }

                var offsets = new NativeArray<int>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                offsets.AsSpan().Fill(-1);
                offsets[frame] = offset;
                m_Clips.Add(new Clip { Hash = clipHash, Frame2Offset = offsets });
            }

            internal void Dispose()
            {
                for (int i = 0; i < m_Clips.Length; i++)
                {
                    m_Clips[i].Frame2Offset.Dispose();
                }
                m_Clips.Dispose();

                m_Persistent.Dispose();
                m_Transient.Dispose();
            }
        }

        static ulong[] s_Hashes = new ulong[256];
        static Entry[] s_Entries = new Entry[256];
        static int s_Count;

        public static Entry Get(BlobAssetReference<JointMeta> jointMeta)
        {
            var jointMetaHash = jointMeta.GetDataHash();
            var mask = s_Entries.Length - 1;
            var index = (int)jointMetaHash & mask;
            while (s_Entries[index] != null)
            {
                if (s_Hashes[index] == jointMetaHash)
                {
                    return s_Entries[index];
                }

                index = (index + 1) & mask;
            }

            var entry = new Entry(jointMeta.Value.Locations.Length);
            s_Hashes[index] = jointMetaHash;
            s_Entries[index] = entry;
            if (++s_Count > s_Entries.Length / 2)
            {
                Grow();
            }

            return entry;
        }

        static void Grow()
        {
            var oldHashes = s_Hashes;
            var oldEntries = s_Entries;
            s_Hashes = new ulong[oldHashes.Length * 2];
            s_Entries = new Entry[oldEntries.Length * 2];
            var mask = s_Hashes.Length - 1;
            for (int i = 0; i < oldEntries.Length; i++)
            {
                var entry = oldEntries[i];
                if (entry == null)
                {
                    continue;
                }

                var index = (int)oldHashes[i] & mask;
                while (s_Entries[index] != null)
                {
                    index = (index + 1) & mask;
                }

                s_Hashes[index] = oldHashes[i];
                s_Entries[index] = entry;
            }
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void RegisterEditorCleanup()
        {
            AppDomain.CurrentDomain.DomainUnload -= Cleanup;
            AppDomain.CurrentDomain.DomainUnload += Cleanup;
        }

        static void Cleanup(object sender, EventArgs e)
        {
            foreach (var entry in s_Entries)
            {
                entry?.Dispose();
            }

            Array.Clear(s_Hashes, 0, s_Hashes.Length);
            Array.Clear(s_Entries, 0, s_Entries.Length);
            s_Count = 0;
        }
#endif
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

        public virtual void Dispose()
        {
            m_View.Dispose();
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
}
