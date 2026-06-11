using System;
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Context;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Game.InGame.Block
{
    public class RendererMaterialReplacer
    {
        /// <summary>
        ///     ゲームオブジェクトのパスに、このリストに含まれる文字列が含まれている場合、マテリアルを置き換えない
        ///     Do not replace materials if the game object's path contains any of the strings in this list.
        /// </summary>
        private readonly List<string> _ignoreGameObjectPathKeyWords = new() { "/VFX/" };
        private readonly Renderer _renderer;
        private readonly Material[] _originalMaterials = Array.Empty<Material>();
        private readonly Dictionary<Material, Material[]> _replacedMaterialsBySource = new();
        private Material[] _currentReplacedMaterials = Array.Empty<Material>();
        private Material _currentSourceMaterial;

        public RendererMaterialReplacer(Renderer renderer)
        {
            var path = renderer.gameObject.GetFullPath();
            foreach (var keyWord in _ignoreGameObjectPathKeyWords)
            {
                if (!path.Contains(keyWord)) continue;
                return;
            }

            _renderer = renderer;
            _originalMaterials = renderer.sharedMaterials;
        }

        public void CopyAndSetMaterial(Material placeMaterial)
        {
            if (_renderer == null) return;
            if (placeMaterial == null) throw new NullReferenceException("The specified material is null.");

            // 同じ材質が適用済みなら再生成も再セットも行わない
            // Skip regeneration and assignment when the same source is already active
            if (_currentSourceMaterial == placeMaterial && _currentReplacedMaterials.Length > 0)
            {
                return;
            }

            // 置換材質は source material ごとに保持して使い回す
            // Cache replacement materials per source material and reuse them
            _currentSourceMaterial = placeMaterial;
            _currentReplacedMaterials = GetOrCreateReplacedMaterials(placeMaterial);
            _renderer.materials = _currentReplacedMaterials;
        }

        public void SetPlaceMaterialProperty(string propertyName, float value)
        {
            foreach (var material in _currentReplacedMaterials) material.SetFloat(propertyName, value);
        }

        public void SetColor(string propertyName, Color color)
        {
            foreach (var material in _currentReplacedMaterials) material.SetColor(propertyName, color);
        }

        public void ResetMaterial()
        {
            if (_renderer == null) return;
            if (_currentReplacedMaterials.Length == 0) return;

            // 表示だけ元材質へ戻し、置換材質は次回用に保持する
            // Restore original materials while keeping cached replacements
            _currentSourceMaterial = null;
            _currentReplacedMaterials = Array.Empty<Material>();
            _renderer.materials = _originalMaterials;
        }

        public void DestroyMaterial()
        {
            // 保持していた runtime clone を対象破棄時にまとめて解放する
            // Release cached runtime clones when the owning object is destroyed
            foreach (var pair in _replacedMaterialsBySource)
            {
                foreach (var material in pair.Value)
                {
                    DestroyCachedMaterial(material);
                }
            }
            _replacedMaterialsBySource.Clear();
            _currentReplacedMaterials = Array.Empty<Material>();
            _currentSourceMaterial = null;
        }

        private Material[] GetOrCreateReplacedMaterials(Material placeMaterial)
        {
            if (_replacedMaterialsBySource.TryGetValue(placeMaterial, out var replacedMaterials))
            {
                CopyOriginalMaterialAppearance(replacedMaterials);
                return replacedMaterials;
            }

            // 元材質の見た目を preview shader 側へ写した clone を作る
            // Create clones that preserve the look of original materials
            replacedMaterials = new Material[_originalMaterials.Length];
            for (var i = 0; i < _originalMaterials.Length; i++)
            {
                replacedMaterials[i] = new Material(placeMaterial);
            }
            CopyOriginalMaterialAppearance(replacedMaterials);
            _replacedMaterialsBySource.Add(placeMaterial, replacedMaterials);
            return replacedMaterials;
        }

        private void CopyOriginalMaterialAppearance(IReadOnlyList<Material> replacedMaterials)
        {
            for (var i = 0; i < _originalMaterials.Length; i++)
            {
                var originalMaterial = _originalMaterials[i];
                var replacedMaterial = replacedMaterials[i];
                replacedMaterial.mainTexture = originalMaterial.mainTexture;
                replacedMaterial.mainTextureOffset = originalMaterial.mainTextureOffset;
                replacedMaterial.mainTextureScale = originalMaterial.mainTextureScale;
                replacedMaterial.color = originalMaterial.color;
            }
        }

        private static void DestroyCachedMaterial(Material material)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(material);
                return;
            }
            Object.DestroyImmediate(material);
        }
    }
}
