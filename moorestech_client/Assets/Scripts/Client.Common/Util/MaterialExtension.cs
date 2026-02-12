using System.Collections.Generic;
using UnityEngine;

namespace Client.Common.Util
{
    public static class MaterialExtension
    {
        private static readonly Material urpMaterial = (Material)Resources.Load("URPLit");
        private static readonly Dictionary<string, string> standardToUrpColor = new() { { "_Color", "_BaseColor" }, { "_EmissionColor", "_EmissionColor" } };
        private static readonly Dictionary<string, string> standardToUrpTexture = new() { { "_MainTex", "_BaseMap" }, { "_BumpMap", "_BumpMap" }, { "_MetallicGlossMap", "_MetallicGlossMap" }, { "_ParallaxMap", "_ParallaxMap" }, { "_OcclusionMap", "_OcclusionMap" } };
        private static readonly Dictionary<string, string> standardToFloat = new() { { "_Metallic", "_Metallic" }, { "_Glossiness", "_Smoothness" } };
        
        public static Material StandardToUrpLit(this Material material)
        {
            if (material.shader.name != "Standard")
            {
                Debug.Log("This material is not Standard:" + material.name);
                return null;
            }
            
            var newMaterial = new Material(urpMaterial);
            newMaterial = CopyProperties(material, newMaterial, standardToUrpColor, standardToUrpTexture, standardToFloat);
            
            return newMaterial;
        }
        
        
        private static Material CopyProperties(Material material, Material newMaterial, Dictionary<string, string> colorIndex, Dictionary<string, string> textureIndex, Dictionary<string, string> floatIndex)
        {
            newMaterial.name = material.name;
            foreach (var index in colorIndex) newMaterial.SetColor(index.Value, material.GetColor(index.Key));
            foreach (var index in colorIndex) newMaterial.SetColor(index.Value, material.GetColor(index.Key));
            foreach (var index in textureIndex)
            {
                newMaterial.SetTexture(index.Value, material.GetTexture(index.Key));
                newMaterial.SetTextureOffset(index.Value, material.GetTextureOffset(index.Key));
                newMaterial.SetTextureScale(index.Value, material.GetTextureScale(index.Key));
            }
            
            foreach (var index in floatIndex) newMaterial.SetFloat(index.Value, material.GetFloat(index.Key));
            return newMaterial;
        }
    }
}