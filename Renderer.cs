using Bastard;
using Unity.Entities;
using UnityEngine;

namespace Graphix
{
    public partial struct Renderer : ISystem
    {
        public static readonly RecycleQueue<Batch> Queue = new();
        private static readonly MaterialPropertyBlock s_MPB = new();

        private static readonly int s_CountEntry = Profile.DefineEntry("Count");
        private static readonly int s_DrawEntry = Profile.DefineEntry("Draw");

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MaterialMeshArray>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var materialMeshArray = MaterialMeshArray.GetInstance(ref state);

            Profile.Set(s_CountEntry, Queue.Count);

            using (new Profile.Scope(s_DrawEntry))
            {
                foreach (var batch in Queue.Drain())
                {
                    s_MPB.Clear();
                    batch.PropertyDrain(s_MPB);

                    var rp = new RenderParams(materialMeshArray.Materials[batch.Material])
                    {
                        matProps = s_MPB
                    };
                    Graphics.RenderMeshInstanced(rp, materialMeshArray.Meshes[batch.Mesh], 0, batch.Worlds.AsArray().Reinterpret<Matrix4x4>(), batch.Count);

                    batch.Worlds.Clear();
                    batch.Count = 0;
                }
            }
        }
    }
}