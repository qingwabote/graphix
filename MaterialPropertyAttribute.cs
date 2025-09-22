using System;

namespace Unity.Rendering
{
    /// <summary>
    /// Marks an IComponentData as an input to a material property on a particular shader.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
    public class MaterialPropertyAttribute : Attribute
    {
        /// <summary>
        /// Constructs a material property attribute.
        /// </summary>
        /// <param name="materialPropertyName">The name of the material property.</param>
        public MaterialPropertyAttribute(string materialPropertyName)
        {
            Name = materialPropertyName;
        }

        /// <summary>
        /// The name of the material property.
        /// </summary>
        public string Name { get; }
    }
}
