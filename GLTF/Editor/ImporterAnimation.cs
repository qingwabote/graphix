using System;
using GLTF.Schema;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
using UnityGLTF;

namespace Graphix.GLTF
{
    public class ImporterAnimation
    {
        public static AnimationClip CreateAnimationClip(GLTFSceneImporter importer, int animationIndex, GameObject scene)
        {
            var gltfAnimation = importer.Root.Animations[animationIndex];
            var gltfChanndels = gltfAnimation.Channels;

            var builder = new BlobBuilder(Allocator.Temp);
            ref Clip clip = ref builder.ConstructRoot<Clip>();
            BlobBuilderArray<Channel> channels = builder.Allocate(ref clip.Channels, gltfChanndels.Count);

            var nodes = new string[gltfChanndels.Count];
            for (int i = 0; i < gltfChanndels.Count; i++)
            {
                var gltfChanndel = gltfChanndels[i];
                var node = importer.NodeCache[gltfChanndel.Target.Node.Id];
                nodes[i] = Utility.RelativePathFrom(node.transform, scene.transform);

                var input_accessor = gltfChanndel.Sampler.Value.Input.Value;
                var input_length = input_accessor.Count;
                BlobBuilderArray<float> input = builder.Allocate(ref channels[i].Input, (int)input_length);
                {
                    var bufferData = importer.AnimationCache[animationIndex].Samplers[gltfChanndel.Sampler.Id].Input.bufferData;
                    unsafe
                    {
                        var input_ptr = (byte*)bufferData.GetUnsafePtr() + input_accessor.ByteOffset + input_accessor.BufferView.Value.ByteOffset;
                        UnsafeUtility.MemCpy(input.GetUnsafePtr(), input_ptr, input_length * 4);
                    }
                }

                var output_accessor = gltfChanndel.Sampler.Value.Output.Value;
                uint output_length;
                switch (output_accessor.Type)
                {
                    case GLTFAccessorAttributeType.VEC3:
                        output_length = output_accessor.Count * 3;
                        break;
                    case GLTFAccessorAttributeType.VEC4:
                        output_length = output_accessor.Count * 4;
                        break;
                    default:
                        throw new Exception($"unsupported output type: {output_accessor.Type}");
                }
                BlobBuilderArray<float> output = builder.Allocate(ref channels[i].Output, (int)output_length);
                {
                    var bufferData = importer.AnimationCache[animationIndex].Samplers[gltfChanndel.Sampler.Id].Output.bufferData;
                    unsafe
                    {
                        var output_ptr = (byte*)bufferData.GetUnsafePtr() + output_accessor.ByteOffset + output_accessor.BufferView.Value.ByteOffset;
                        UnsafeUtility.MemCpy(output.GetUnsafePtr(), output_ptr, output_length * 4);
                    }
                }

                switch (gltfChanndel.Target.Path)
                {
                    case "translation":
                        channels[i].Path = ChannelPath.TRANSLATION;
                        break;
                    case "rotation":
                        channels[i].Path = ChannelPath.ROTATION;
                        break;
                    case "scale":
                        channels[i].Path = ChannelPath.SCALE;
                        break;
                    case "weights":
                    default:
                        throw new Exception($"unsupported channel path: {gltfChanndel.Target.Path}");
                }
            }

            var animationClip = ScriptableObject.CreateInstance<AnimationClip>();
            animationClip.name = gltfAnimation.Name;
            animationClip.Nodes = nodes;
            animationClip.Blob = builder.CreateBlobAssetReference<Clip>(Allocator.Persistent);
            builder.Dispose();

            return animationClip;
        }
    }
}