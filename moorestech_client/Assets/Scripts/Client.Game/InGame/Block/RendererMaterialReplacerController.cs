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
            foreach (var renderer in targetObject.GetComponentsInChildren<Renderer>())
            {
                _rendererMaterialReplacers.Add(new RendererMaterialReplacer(renderer));
            }
        }
        
        public void CopyAndSetMaterial(Material placeMaterial)
        {
            _rendererMaterialReplacers.ForEach(replacer => replacer.CopyAndSetMaterial(placeMaterial));
            GameObject.Destroy(placeMaterial);
        }
        
        public void SetPlaceMaterialProperty(string propertyName, float value)
        {
            _rendererMaterialReplacers.ForEach(replacer => replacer.SetPlaceMaterialProperty(propertyName, value));
        }
        
        public void SetColor(Color color)
        {
            _rendererMaterialReplacers.ForEach(replacer => replacer.SetColor(color));
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