using System.Collections.Generic;
using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class RendererMaterialReplacer
    {
        /// <summary>
        ///     ゲームオブジェクトのパスに、このリストに含まれる文字列が含まれている場合、マテリアルを置き換えない
        ///     Do not replace materials if the game object's path contains any of the strings in this list.
        /// </summary>
        private readonly List<string> _ignoreGameObjectPathKeyWords = new() { "/VFX/" };
        private readonly List<Material> _originalMaterials;
        private readonly Renderer _renderer;
        private readonly List<Material> _replacedMaterials = new();

        public RendererMaterialReplacer(Renderer renderer)
        {
            var path = renderer.gameObject.GetFullPath();
            foreach (var keyWord in _ignoreGameObjectPathKeyWords)
            {
                if (!path.Contains(keyWord)) continue;
                return;
            }


            _renderer = renderer;
            _originalMaterials = new List<Material>();
            foreach (var material in renderer.sharedMaterials)
            {
                _originalMaterials.Add(material);
            }
        }

        public void SetMaterial(Material placeMaterial)
        {
            if (_renderer == null)
            {
                return;
            }

            foreach (var material in _renderer.sharedMaterials)
            {
                var mainTexture = material.mainTexture;
                var mainColor = material.color;

                var newMaterial = new Material(placeMaterial)
                {
                    mainTexture = mainTexture,
                    mainTextureOffset = material.mainTextureOffset,
                    mainTextureScale = material.mainTextureScale,
                    color = mainColor,
                };

                _replacedMaterials.Add(newMaterial);
            }

            _renderer.materials = _replacedMaterials.ToArray();
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
            if (_renderer == null)
            {
                return;
            }

            //作ったプレビュー用のマテリアルを削除
            foreach (var material in _replacedMaterials)
            {
                Object.Destroy(material);
            }
            _replacedMaterials.Clear();
            _renderer.materials = _originalMaterials.ToArray();
        }
    }
}