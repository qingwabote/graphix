using Bastard;
using Graphix;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace Graphix
{
    public partial struct EntitiesGraphicsSystemUnmanaged : ISystem
    {
        private struct QueuesTag { }
        internal static readonly SharedStatic<Bastard.UnsafeHashMap<int, UnsafeList<Batch>>> s_Queues = SharedStatic<Bastard.UnsafeHashMap<int, UnsafeList<Batch>>>.GetOrCreate<QueuesTag>();

        public unsafe static ref UnsafeList<Batch> GetQueue(int materialMeshArray)
        {
            var queue = s_Queues.Data.EnsureValuePtr(materialMeshArray, out var uninitialized);
            if (uninitialized)
            {
                *queue = new(32, Allocator.Temp);
            }
            return ref UnsafeUtility.AsRef<UnsafeList<Batch>>(queue);
        }

        public void OnUpdate(ref SystemState state)
        {
            s_Queues.Data = new(2, Allocator.Temp);
        }
    }
}

namespace Unity.Rendering
{
    public partial class EntitiesGraphicsSystem : SystemBase
    {
#if UNITY_EDITOR
        public static bool SceneViewShowsRuntime;
#endif

        private static readonly Registry<Material> s_Materials;
        private static readonly Registry<Mesh> s_Meshes;

        private static readonly MaterialPropertyBlock s_MPB = new();

        static EntitiesGraphicsSystem()
        {
            s_Materials = new();
            s_Materials.Register(null);
            s_Meshes = new();
            s_Meshes.Register(null);
        }

        public static void GetRenderContext(out Camera camera, out ulong sceneCullingMask, out bool overrideSceneCullingMask)
        {
            camera = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            camera = UnityEngine.Camera.main; // Explicit camera for the RenderGroup of Unity6 with WX SDK
#endif

            sceneCullingMask = 0;
            overrideSceneCullingMask = false;
#if UNITY_EDITOR
            if (!SceneViewShowsRuntime)
            {
                sceneCullingMask = UnityEditor.SceneManagement.SceneCullingMasks.GameViewObjects;
                overrideSceneCullingMask = true;
            }
#endif
        }

        public int RegisterMaterial(Material material)
        {
            return s_Materials.Register(material);
        }

        public int RegisterMesh(Mesh mesh)
        {
            return s_Meshes.Register(mesh);
        }

        protected override void OnCreate()
        {
            MaterialProperty.Initialize(EntityManager);

            RequireForUpdate<MaterialMeshArray>();
        }

        protected override void OnUpdate()
        {
            int batchCount = 0;
            int instanceCount = 0;
            GetRenderContext(out var camera, out var sceneCullingMask, out var overrideSceneCullingMask);

            foreach (var kv in EntitiesGraphicsSystemUnmanaged.s_Queues.Data)
            {
                var materialMeshArray = kv.Key != -1 ? EntityManager.GetSharedComponentManaged<MaterialMeshArray>(kv.Key) : default;
                ref var queue = ref kv.Value;

                batchCount += queue.Length;

                foreach (var batch in queue)
                {
                    var material = batch.Material < 0 ? materialMeshArray.Materials[-batch.Material] : s_Materials.Get(batch.Material);
                    var mesh = batch.Mesh < 0 ? materialMeshArray.Meshes[-batch.Mesh] : s_Meshes.Get(batch.Mesh);
                    if (material == null || mesh == null)
                    {
                        continue;
                    }

                    if (material.enableInstancing)
                    {
                        s_MPB.Clear();
                        batch.PropertyToBlock(s_MPB);
                        var rp = new RenderParams(material)
                        {
                            camera = camera,
                            sceneCullingMask = sceneCullingMask,
                            overrideSceneCullingMask = overrideSceneCullingMask,
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
                                    camera = camera,
                                    sceneCullingMask = sceneCullingMask,
                                    overrideSceneCullingMask = overrideSceneCullingMask,
                                    matProps = s_MPB
                                };
                                Graphics.RenderMesh(rp, mesh, 0, batch.LocalToWorlds.ElementAt(i));
                            }
                        }
                        else
                        {
                            var rp = new RenderParams(material)
                            {
                                camera = camera,
                                sceneCullingMask = sceneCullingMask,
                                overrideSceneCullingMask = overrideSceneCullingMask,
                            };
                            for (int i = 0; i < batch.Count; i++)
                            {
                                Graphics.RenderMesh(rp, mesh, 0, batch.LocalToWorlds.ElementAt(i));
                            }
                        }

                    }
                    instanceCount += batch.Count;
                }
            }
        }
    }
}
