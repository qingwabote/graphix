using System.Collections.Generic;
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

        private static int s_CountEntry = Profile.DefineEntry("Count");
        private static int s_DrawEntry = Profile.DefineEntry("Draw");

        public static bool Register(int key, out Batch batch)
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
                    foreach (var (id, texture) in batch.MaterialProperty.Textures)
                    {
                        s_MPB.SetTexture(id, texture);
                    }
                    foreach (var (id, list) in batch.MaterialProperty.Floats)
                    {
                        s_MPB.SetFloatArray(id, list);
                    }

                    var rp = new RenderParams(materials[batch.Material])
                    {
                        matProps = s_MPB
                    };
                    Graphics.RenderMeshInstanced(rp, meshes[batch.Mesh], 0, batch.InstanceWorlds.AsArray().Reinterpret<Matrix4x4>(), batch.InstanceCount);

                    batch.MaterialProperty.Clear();
                    batch.InstanceWorlds.Clear();
                    batch.InstanceCount = 0;
                }
                s_Cache.Clear();
            }
        }

        public int Mesh;
        public int Material;
        public MaterialProperty MaterialProperty = new();

        public NativeList<float4x4> InstanceWorlds = new(Allocator.Persistent);
        public int InstanceCount;
    }
}