using Bastard;
using Unity.Entities;
using UnityEngine;

namespace Graphix
{
    public struct ChannelTarget : IBufferElementData, IEnableableComponent
    {
        public Entity Value;
    }

    public struct ClipBinging : IBufferElementData
    {
        public BlobAssetReference<Clip> Blob;
        public int TargetIndex;
    }

    class AnimationAuthoring : MonoBehaviour
    {
        public AnimationClip[] Clips;
        public int Index;
    }

    public struct AnimationState : IComponentData
    {
        public int Index;
        public float Time;
    }

    class AnimationBaker : Baker<AnimationAuthoring>
    {
        public override void Bake(AnimationAuthoring authoring)
        {
            if (authoring.Clips == null)
            {
                return;
            }

            foreach (var clip in authoring.Clips)
            {
                if (clip == null)
                {
                    return;
                }
            }

            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var targets = AddBuffer<ChannelTarget>(entity);
            var bindings = AddBuffer<ClipBinging>(entity);
            foreach (var clip in authoring.Clips)
            {
                bindings.Add(new ClipBinging { Blob = clip.Blob, TargetIndex = targets.Length });
                foreach (var path in clip.Nodes)
                {
                    // Why use TransformUsageFlags.None, see TargetStripper
                    targets.Add(new ChannelTarget { Value = GetEntity(authoring.transform.GetChildByPath(path), TransformUsageFlags.None) });
                }
            }

            AddComponent(entity, new AnimationState { Index = authoring.Index });
        }
    }
}