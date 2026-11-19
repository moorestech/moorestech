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
                
                // レンダラーが IgnoreRendererMaterialReplacer の子である場合は無視。枠を指定したものは同じ GameObject のレンダラーのその枠だけ残す
                // Ignore if the renderer is a child of the IgnoreRendererMaterialReplacer; one with slots keeps only those slots of the renderer on its GameObject
                var isIgnore = false;
                IReadOnlyList<int> keptMaterialSlots = Array.Empty<int>();
                foreach (var ignoreParent in ignoreParents)
                {
                    if (ignoreParent.MaterialSlots.Count == 0)
                    {
                        isIgnore |= renderer.transform.IsChildOf(ignoreParent.transform);
                        continue;
                    }
                    if (ignoreParent.gameObject == renderer.gameObject) keptMaterialSlots = ignoreParent.MaterialSlots;
                }
                if (isIgnore) continue;

                _rendererMaterialReplacers.Add(new RendererMaterialReplacer(renderer, keptMaterialSlots));
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