using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class RendererMaterialReplacerController
    {
        private readonly List<RendererMaterialReplacer> _rendererMaterialReplacers;
        
        public RendererMaterialReplacerController(GameObject targetObject)
        {
            _rendererMaterialReplacers = new List<RendererMaterialReplacer>();
            var ignoreParents = targetObject.GetComponentsInChildren<IgnoreRendererMaterialReplacer>(true);
            
            foreach (var renderer in targetObject.GetComponentsInChildren<Renderer>())
            {
                // TextMeshProはエラーになるので無視
                // Ignore TextMeshPro because it causes an error
                if (renderer.GetComponent<TMPro.TextMeshPro>()) continue;
                
                // レンダラーが IgnoreRendererMaterialReplacer の子である場合は無視
                // Ignore if the renderer is a child of the IgnoreRendererMaterialReplacer
                var isIgnore = false;
                foreach (var ignoreParent in ignoreParents)
                {
                    isIgnore |= renderer.transform.IsChildOf(ignoreParent.transform);
                }
                if (isIgnore) continue;
                
                _rendererMaterialReplacers.Add(new RendererMaterialReplacer(renderer));
            }
        }
        
        public void CopyAndSetMaterial(Material placeMaterial)
        {
            _rendererMaterialReplacers.ForEach(replacer => replacer.CopyAndSetMaterial(placeMaterial));
        }
        
        public void SetPlaceMaterialProperty(string propertyName, float value)
        {
            _rendererMaterialReplacers.ForEach(replacer => replacer.SetPlaceMaterialProperty(propertyName, value));
        }
        
        public void SetColor(string propertyName, Color color)
        {
            _rendererMaterialReplacers.ForEach(replacer => replacer.SetColor(propertyName, color));
        }
        
        public void ResetMaterial()
        {
            _rendererMaterialReplacers.ForEach(replacer => replacer.ResetMaterial());
        }
        
        public void DestroyMaterial()
        {
            _rendererMaterialReplacers.ForEach(replacer => replacer.DestroyMaterial());
        }
    }
}