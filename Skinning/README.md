A vertex shader skinning implementation

# Hierarchy Optimization
[The transform of the node that the skin is attached to is ignored](https://github.khronos.org/glTF-Tutorials/gltfTutorial/gltfTutorial_020_Skins.html), graphix uses the root entity to hold all MaterialMeshes. The other entities(bones) have only one component, the LocalTransform for the animation system. It breaks the rule "one entity one render object"


*https://docs.unity.cn/cn/tuanjiemanual/Manual/GpuSkinning.html*