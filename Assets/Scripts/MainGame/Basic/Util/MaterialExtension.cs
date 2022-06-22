using System.Collections.Generic;
using UnityEngine;

namespace MainGame.Basic.Util
{
    public static class MaterialExtension
    {
        private static readonly Material urpMaterial = (Material)Resources.Load("URPLit");
        private static List<string> standardToUrpColor = new() { };
        public static Material StandardToUrpLit(this Material material)
        {
            if (material.shader.name != "Standard")
            {
                Debug.Log("このマテリアルはStandardではありません :" + material.name);
                return null;
            }
            
            var newMaterial = new Material(urpMaterial);
            
            newMaterial
            
            return newMaterial;
        }
    }
}