**Entities Graphics** requires compute shaders, which WebGL does not support. This library provides an alternative implementation using the same interface.

> [微信小游戏案例](https://jotdown.pages.dev/#/?id=unity-minigame-showcase)

## Render Object
There are two types of render objects
- MaterialMeshInfo for IComponentData
- MaterialMeshInfoBuffered for IBufferElementData

The MaterialPropertyAttribute can be used on IComponentData or IBufferElementData to support both