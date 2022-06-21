using UnityEngine;

namespace MainGame.Basic.Util
{
    public static class MaterialExtension
    {
        public static Material CopyMaterial(this Material material, Material shader)
        {
            var newMaterial = new Material(shader);
            newMaterial.CopyPropertiesFromMaterial(material);
            newMaterial.shader = shader.shader;
            return newMaterial;
        }
    }
}