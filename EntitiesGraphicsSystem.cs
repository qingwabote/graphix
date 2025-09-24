using Bastard;
using Graphix;
using Unity.Entities;
using UnityEngine;

namespace Unity.Rendering
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial class EntitiesGraphicsSystem : SystemBase
    {
        private static readonly Registry<Material> s_Materials;

        public static readonly RecycleQueue<Batch> Queue = new();

        private static readonly MaterialPropertyBlock s_MPB = new();

        private static readonly int s_Batches = Profile.DefineEntry("Batches");
        private static readonly int s_Entities = Profile.DefineEntry("Entities");
        private static readonly int s_DrawEntry = Profile.DefineEntry("Draw");

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
            using (new Profile.Scope(s_DrawEntry))
            {
                var materialMeshArray = MaterialMeshArray.GetInstance(EntityManager);

                Profile.Delta(s_Batches, Queue.Count);

                int entities = 0;
                foreach (var batch in Queue.Drain())
                {
                    s_MPB.Clear();
                    batch.PropertyDrain(s_MPB);

                    var rp = new RenderParams(batch.Material < 0 ? materialMeshArray.Materials[-batch.Material] : s_Materials.Get(batch.Material))
                    {
                        matProps = s_MPB
                    };
                    Graphics.RenderMeshInstanced(rp, materialMeshArray.Meshes[-batch.Mesh], 0, batch.Worlds.AsArray().Reinterpret<Matrix4x4>(), batch.Count);
                    entities += batch.Count;

                    batch.Worlds.Clear();
                    batch.Count = 0;
                }
                Profile.Delta(s_Entities, entities);
            }
        }
    }
}