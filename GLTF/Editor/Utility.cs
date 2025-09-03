using System;
using System.Collections.Generic;
using UnityEngine;

namespace Graphix.GLTF
{
    public class Utility
    {
        public static string RelativePathFrom(Transform self, Transform root)
        {
            var path = new List<string>();
            for (var current = self; current != null; current = current.parent)
            {
                if (current == root)
                {
                    return string.Join("/", path.ToArray());
                }

                path.Insert(0, current.name);
            }

            throw new Exception("no RelativePath");
        }
    }
}