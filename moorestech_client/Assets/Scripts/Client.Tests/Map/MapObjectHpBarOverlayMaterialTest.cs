using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Tests.Map
{
    /// <summary>
    ///     HPバー最前面描画の構成を検証（ADR0031）
    ///     Verifies the HP bar's front-most draw material setup (ADR 0031)
    /// </summary>
    public class MapObjectHpBarOverlayMaterialTest
    {
        private const string HpBarPrefabPath = "Assets/Asset/Environment/Prefab/MapObjectHpBar.prefab";
        private const string ImageOverlayShaderPath = "Assets/Asset/Common/Shader/UI/UIOverlay.shader";
        private const string ImageOverlayMaterialPath = "Assets/Asset/Common/Shader/UI/UIOverlay.mat";
        private const string TextOverlayMaterialPath = "Assets/Asset/Environment/Prefab/MapObjectHpBarText.mat";

        // ZTest Alwaysを焼いたシェーダ。既定のUI/DefaultはWorld Space CanvasでLEqualになり必ずジオメトリに沈む
        // Shaders with ZTest Always baked in; the stock UI/Default falls back to LEqual on a World Space Canvas and always sinks behind geometry
        private const string ImageOverlayShaderName = "UI/Overlay";
        private const string TextOverlayShaderName = "TextMeshPro/Mobile/Distance Field Overlay";

        [Test]
        public void HPバーのImageが最前面描画シェーダで描かれる()
        {
            var prefab = LoadHpBarPrefab();
            var expectedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ImageOverlayMaterialPath);
            Assert.IsNotNull(expectedMaterial, $"overlay material not found: {ImageOverlayMaterialPath}");

            var images = prefab.GetComponentsInChildren<Image>(true);
            Assert.IsNotEmpty(images, "hp bar prefab has no Image to render");
            foreach (var image in images)
            {
                Assert.AreEqual(ImageOverlayShaderName, image.material.shader.name, $"{image.name} lost its overlay material");
                Assert.AreSame(expectedMaterial, image.material, $"{image.name} references a different material than the canonical overlay material");
            }
        }

        [Test]
        public void HP数値が最前面描画シェーダで描かれる()
        {
            var prefab = LoadHpBarPrefab();
            var expectedMaterial = AssetDatabase.LoadAssetAtPath<Material>(TextOverlayMaterialPath);
            Assert.IsNotNull(expectedMaterial, $"overlay material not found: {TextOverlayMaterialPath}");

            var texts = prefab.GetComponentsInChildren<TMP_Text>(true);
            Assert.IsNotEmpty(texts, "hp bar prefab has no TMP_Text");
            foreach (var text in texts)
            {
                Assert.AreEqual(TextOverlayShaderName, text.fontSharedMaterial.shader.name, $"{text.name} lost its overlay font material");
                Assert.AreSame(expectedMaterial, text.fontSharedMaterial, $"{text.name} references a different material than the canonical overlay font material");

                // アトラス再生成に追従できているか、font asset本体の値と突き合わせる
                // Cross-check against the font asset itself so atlas regeneration is not silently missed
                var defaultMaterial = text.font.material;
                Assert.AreEqual(text.font.atlasWidth, text.fontSharedMaterial.GetFloat("_TextureWidth"), $"{text.name} overlay material texture width drifted from its font asset");
                Assert.AreEqual(text.font.atlasHeight, text.fontSharedMaterial.GetFloat("_TextureHeight"), $"{text.name} overlay material texture height drifted from its font asset");
                Assert.AreEqual(defaultMaterial.GetFloat("_GradientScale"), text.fontSharedMaterial.GetFloat("_GradientScale"), $"{text.name} overlay material gradient scale drifted from its font asset");
            }
        }

        [Test]
        public void HPバーのシェーダが全ジオメトリを貫通する描画ステートを持つ()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ImageOverlayShaderPath);
            Assert.IsNotNull(shader, $"overlay shader not found: {ImageOverlayShaderPath}");

            var source = File.ReadAllText(AssetDatabase.GetAssetPath(shader));
            StringAssert.IsMatch(@"ZTest\s+Always", source, "overlay shader lost ZTest Always and will sink behind geometry again");
            StringAssert.IsMatch(@"ZWrite\s+Off", source, "overlay shader lost ZWrite Off");
            StringAssert.IsMatch(@"""Queue""\s*=\s*""Overlay""", source, "overlay shader left the Overlay queue and can be painted over by world-space transparents");
        }

        private static GameObject LoadHpBarPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HpBarPrefabPath);
            Assert.IsNotNull(prefab, $"hp bar prefab not found: {HpBarPrefabPath}");
            return prefab;
        }
    }
}
