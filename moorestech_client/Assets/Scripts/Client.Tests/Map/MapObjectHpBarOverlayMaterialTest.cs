using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Tests.Map
{
    /// <summary>
    ///     HPバーが3Dジオメトリの手前に描かれるマテリアル構成であることを検証（ADR 0031）
    ///     Verifies the HP bar keeps the material setup that draws it in front of 3D geometry (ADR 0031)
    /// </summary>
    public class MapObjectHpBarOverlayMaterialTest
    {
        private const string HpBarPrefabPath = "Assets/Asset/Environment/Prefab/MapObjectHpBar.prefab";

        // ZTest Alwaysを焼いたシェーダ。既定のUI/DefaultはWorld Space CanvasでLEqualになり必ずジオメトリに沈む
        // Shaders with ZTest Always baked in; the stock UI/Default falls back to LEqual on a World Space Canvas and always sinks behind geometry
        private const string ImageOverlayShaderName = "UI/Overlay";
        private const string TextOverlayShaderName = "TextMeshPro/Mobile/Distance Field Overlay";

        [Test]
        public void HPバーのImageが最前面描画シェーダで描かれる()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HpBarPrefabPath);
            Assert.IsNotNull(prefab, $"hp bar prefab not found: {HpBarPrefabPath}");

            var images = prefab.GetComponentsInChildren<Image>(true);
            Assert.IsNotEmpty(images, "hp bar prefab has no Image to render");
            foreach (var image in images)
                Assert.AreEqual(ImageOverlayShaderName, image.material.shader.name, $"{image.name} lost its overlay material");
        }

        [Test]
        public void HP数値が最前面描画シェーダで描かれる()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HpBarPrefabPath);
            var text = prefab.GetComponentInChildren<TMP_Text>(true);
            Assert.IsNotNull(text, "hp bar prefab has no TMP_Text");
            Assert.AreEqual(TextOverlayShaderName, text.fontSharedMaterial.shader.name, "hp text lost its overlay font material");
        }
    }
}
