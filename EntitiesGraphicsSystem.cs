using Bag;
using Unity.Entities;
using UnityEngine;

namespace Unity.Rendering
{
    public partial class EntitiesGraphicsSystem : SystemBase
    {
#if UNITY_EDITOR
        public static bool SceneViewShowsRuntime;
#endif

        private static readonly MaterialPropertyBlock s_MPB = new();

        private BatchQueue m_Queue;
        public BatchQueue Queue => m_Queue;

        protected override void OnCreate()
        {
            m_Queue = World.GetExistingSystemManaged<BatchGroup>().CreateQueue();
        }

        protected override void OnDestroy()
        {
            m_Queue.Dispose();
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

        public UnityObjectRef<Material> RegisterMaterial(Material material)
        {
            return material;
        }

        public UnityObjectRef<Mesh> RegisterMesh(Mesh mesh)
        {
            return mesh;
        }

        protected override void OnUpdate()
        {
            int batchCount = 0;
            int instanceCount = 0;
            GetRenderContext(out var camera, out var sceneCullingMask, out var overrideSceneCullingMask);

            using var batches = m_Queue.Dump();

            batchCount += batches.Length;

            for (int index = 0; index < batches.Length; index++)
            {
                ref readonly var batch = ref batches.ElementAt(index);

                var material = (Material)batch.Material;
                var mesh = (Mesh)batch.Mesh;
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
