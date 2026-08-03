using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Client.Tests.Localization
{
    public class SerializedLocalizedTooltipKeyTest
    {
        [Test]
        public void Prefab内の固定翻訳TooltipKeyはバニラ辞書に存在する()
        {
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            foreach (var prefabGuid in prefabGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (root == null) continue;

                // import済みPrefabを検査する。
                // Inspect only imported prefab assets.
                AssertGameObjectTooltipKeys(root, assetPath);
                AssertUGuiTooltipKeys(root, assetPath);
            }
        }

        private static void AssertGameObjectTooltipKeys(GameObject root, string assetPath)
        {
            foreach (var target in root.GetComponentsInChildren<GameObjectTooltipTarget>(true))
            {
                var serializedTarget = new SerializedObject(target);
                AssertKnownNonEmptyKey(serializedTarget.FindProperty("textKey").stringValue, assetPath);
            }
        }

        private static void AssertUGuiTooltipKeys(GameObject root, string assetPath)
        {
            foreach (var target in root.GetComponentsInChildren<UGuiTooltipTarget>(true))
            {
                var serializedTarget = new SerializedObject(target);
                AssertKnownNonEmptyKey(serializedTarget.FindProperty("textKey").stringValue, assetPath);
            }
        }

        private static void AssertKnownNonEmptyKey(string textKey, string assetPath)
        {
            if (string.IsNullOrEmpty(textKey)) return;
            Assert.IsTrue(
                VanillaLocalizationTable.SourceTexts.ContainsKey(textKey),
                $"Unknown localized tooltip key '{textKey}' in {assetPath}");
        }
    }
}
