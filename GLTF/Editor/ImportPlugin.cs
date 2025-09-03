using System.Collections.Generic;
using GLTF.Schema;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityGLTF.Plugins;

namespace Graphix.GLTF
{
    public class ImportPluginContext : GLTFImportPluginContext
    {
        private GLTFImportContext _context;

        public ImportPluginContext(GLTFImportContext context)
        {
            _context = context;
        }

        public override void OnBeforeImport()
        {
            _context.SceneImporter.CustomShaderName = "Graphix/Phong";
        }

        public override void OnAfterImportNode(Node node, int nodeIndex, GameObject nodeObject)
        {
            var renderer = nodeObject.GetComponent<UnityEngine.Renderer>();
            if (renderer)
            {
                if (renderer is UnityEngine.MeshRenderer)
                {
                    nodeObject.AddComponent<MeshRendererAuthoring>();
                }
            }
        }

        public override void OnAfterImportMaterial(GLTFMaterial material, int materialIndex, Material materialObject)
        {
            var pbr = material.PbrMetallicRoughness;
            if (pbr != null)
            {
                var baseColor = pbr.BaseColorFactor;
                if (baseColor != null)
                {
                    materialObject.SetColor("_BaseColor", new(baseColor.R, baseColor.G, baseColor.B, baseColor.A));
                }
                var baseColorTexture = pbr.BaseColorTexture;
                if (baseColorTexture != null)
                {
                    materialObject.SetTexture("_BaseMap", _context.SceneImporter.TextureCache[baseColorTexture.Index.Id].Texture);
                    materialObject.SetFloat("_BASEMAP", 1);
                }

                float smoothness = 1.0f - (float)pbr.RoughnessFactor;
                materialObject.SetFloat("_Smoothness", smoothness);

                // ignore Metallic
            }
        }

        public override void OnAfterImportScene(GLTFScene scene, int sceneIndex, GameObject sceneObject)
        {
            if (_context.Root.Animations != null)
            {
                for (int i = 0; i < _context.Root.Animations.Count; i++)
                {
                    var animationClip = ImporterAnimation.CreateAnimationClip(_context.SceneImporter, i, sceneObject);
                    _context.AssetContext.AddObjectToAsset($"Budget_{animationClip.name}", animationClip);
                }

            }
            if (_context.Root.Skins != null)
            {
                var schemaSkin = _context.Root.Skins[0];

                var skinnedRenderer = sceneObject.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();

                var skin = ScriptableObject.CreateInstance<Skin>();
                skin.name = schemaSkin.Name ?? "Skin_0";

                var joints = new string[skinnedRenderer.bones.Length];
                for (int i = 0; i < joints.Length; i++)
                {
                    joints[i] = Utility.RelativePathFrom(skinnedRenderer.bones[i].transform, sceneObject.transform);
                }
                skin.Joints = joints;
                {
                    var accessor = schemaSkin.InverseBindMatrices.Value;

                    var builder = new BlobBuilder(Allocator.Temp);
                    ref var inverseBindMatrices = ref builder.ConstructRoot<InverseBindMatrices>();
                    var data = builder.Allocate(ref inverseBindMatrices.Data, (int)accessor.Count);
                    var bindposes = skinnedRenderer.sharedMesh.bindposes;
                    for (int i = 0; i < bindposes.Length; i++)
                    {
                        data[i] = bindposes[i];
                    }
                    // Is there a cleaner way to access bufferData?
                    // var bufferData = _context.SceneImporter.AnimationCache[0].Samplers[0].Input.bufferData;
                    // unsafe
                    // {
                    //     var source = (byte*)bufferData.GetUnsafePtr() + accessor.ByteOffset + accessor.BufferView.Value.ByteOffset;
                    //     UnsafeUtility.MemCpy(data.GetUnsafePtr(), source, 64 * accessor.Count);
                    // }
                    skin.InverseBindMatrices = builder.CreateBlobAssetReference<InverseBindMatrices>(Allocator.Persistent);
                    builder.Dispose();
                }

                var skinAuthoring = sceneObject.AddComponent<SkinAuthoring>();
                skinAuthoring.Proto = skin;

                var materials = new Dictionary<Material, Material>();
                var skinnedRenderers = sceneObject.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var renderer in skinnedRenderers)
                {
                    if (!materials.TryGetValue(renderer.sharedMaterial, out Material material))
                    {
                        material = new Material(renderer.sharedMaterial)
                        {
                            shader = Shader.Find("Graphix/Phong"),
                            enableInstancing = true
                        };
                        material.SetFloat("_SKINNING", 1);
                        materials.Add(renderer.sharedMaterial, material);

                        _context.AssetContext.AddObjectToAsset($"Budget_{material.name}", material);
                    }
                    var authoring = renderer.gameObject.AddComponent<SkinnedMeshRendererAuthoring>();
                    authoring.Material = material;
                    authoring.Skin = skinAuthoring;
                }

                _context.AssetContext.AddObjectToAsset($"Budget_{skin.name}", skin);
            }
        }
    }

    public class ImportPlugin : GLTFImportPlugin
    {
        public override string DisplayName => "Graphix.GLTF.ImportPlugin";

        public override GLTFImportPluginContext CreateInstance(GLTFImportContext context)
        {
            return new ImportPluginContext(context);
        }
    }
}

