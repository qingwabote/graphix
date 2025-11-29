using Bastard;
using Graphix;
using Unity.Entities;
using UnityEngine;

namespace Unity.Rendering
{
    public partial class EntitiesGraphicsSystem : SystemBase
    {
        private static readonly Registry<Material> s_Materials;

        public static readonly RecycleQueue<Batch> Queue = new();

        private static readonly MaterialPropertyBlock s_MPB = new();

        private static readonly int s_Batches = Profile.DefineEntry("Batches");
        private static readonly int s_Entities = Profile.DefineEntry("Instances");
        private static readonly int s_Graphics = Profile.DefineEntry("Graphics");

        static EntitiesGraphicsSystem()
        {
            s_Materials = new();
            s_Materials.Register(null);
        }

        public int RegisterMaterial(Material material)
        {
            return s_Materials.Register(material);
        }

        public int RegisterMesh(Mesh mesh)
        {
            return 0;
        }

        protected override void OnCreate()
        {
            MaterialProperty.Initialize(EntityManager);

            RequireForUpdate<MaterialMeshArray>();
        }

        protected override void OnUpdate()
        {
            using (new Profile.Scope(s_Graphics))
            {
                var materialMeshArray = MaterialMeshArray.GetCurrent(EntityManager);

                Profile.Delta(s_Batches, Queue.Count);

                int instances = 0;
                foreach (var batch in Queue.Drain())
                {
                    var material = batch.Material < 0 ? materialMeshArray.Materials[-batch.Material] : s_Materials.Get(batch.Material);
                    var mesh = materialMeshArray.Meshes[-batch.Mesh];
                    if (material.enableInstancing)
                    {
                        s_MPB.Clear();
                        batch.PropertyToBlock(s_MPB);
                        var rp = new RenderParams(material)
                        {
                            matProps = s_MPB
                        };
                        Graphics.RenderMeshInstanced(rp, mesh, 0, batch.LocalToWorlds.AsArray().Reinterpret<Matrix4x4>(), batch.Count);
                    }
                    else
                    {
                        if (batch.PropertyAcquired)
                        {
                            for (int i = 0; i < batch.Count; i++)
                            {
                                s_MPB.Clear();
                                batch.PropertyToBlock(i, s_MPB);
                                var rp = new RenderParams(material)
                                {
                                    matProps = s_MPB
                                };
                                Graphics.RenderMesh(rp, mesh, 0, batch.LocalToWorlds.ElementAt(i));
                            }
                        }
                        else
                        {
                            var rp = new RenderParams(material);
                            for (int i = 0; i < batch.Count; i++)
                            {
                                Graphics.RenderMesh(rp, mesh, 0, batch.LocalToWorlds.ElementAt(i));
                            }
                        }

                    }
                    instances += batch.Count;
                    batch.Clear();
                }
                Profile.Delta(s_Entities, instances);
            }
        }
    }
}