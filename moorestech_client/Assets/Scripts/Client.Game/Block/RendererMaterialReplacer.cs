using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Game.Block
{
    public class RendererMaterialReplacer
    {
        private readonly Renderer _renderers;
        private readonly List<Material> _originalMaterials;
        private readonly List<Material> _replacedMaterials = new();

        public RendererMaterialReplacer(Renderer renderers)
        {
            _renderers = renderers;
            _originalMaterials = new List<Material>();
            foreach (var material in renderers.sharedMaterials)
            {
                _originalMaterials.Add(material);
            }
        }
        
        public void SetMaterial(Material placeMaterial)
        {
            foreach (var material in _renderers.sharedMaterials)
            {
                var mainTexture = material.mainTexture;
                var mainColor = material.color;

                var newMaterial = new Material(placeMaterial)
                {
                    mainTexture = mainTexture,
                    mainTextureOffset = material.mainTextureOffset,
                    mainTextureScale = material.mainTextureScale,
                    color = mainColor
                };

                _replacedMaterials.Add(newMaterial);
            }

            _renderers.materials = _replacedMaterials.ToArray();
        }
        
        public void SetPlaceMaterialProperty(string propertyName, float value)
        {
            foreach (var material in _replacedMaterials)
            {
                material.SetFloat(propertyName, value);
            }
        }

        public void ResetMaterial()
        {
            //作ったプレビュー用のマテリアルを削除
            foreach (var material in _replacedMaterials)
            {
                Object.Destroy(material);
            }
            _replacedMaterials.Clear();
            _renderers.materials = _originalMaterials.ToArray();
        }
    }
}