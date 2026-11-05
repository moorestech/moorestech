using System;
using Client.Ui.Foundation.Assets;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Assets
{
    /// <summary>
    /// 本番の USS カタログ資産が全フィールドに実在する USS を持つこと、欠けを全部並べて止めることを確かめる（L2）
    /// Verifies the production USS catalog asset references a real USS in every field, and that missing entries are all listed (L2)
    /// </summary>
    public class UiAssetCatalogTest
    {
        private const string CatalogPath = "Assets/UI Toolkit/GameUi/GameUiAssetCatalog.asset";

        [Test]
        public void ProductionCatalogHasEveryStyleSheet()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UiAssetCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, CatalogPath);
            Assert.DoesNotThrow(catalog.ValidateAllAssigned);

            // 検査表に載せ忘れたフィールドも拾うため、シリアライズされた参照を全部たどる
            // Walk every serialized reference so a field missing from the validation table is caught too
            var serialized = new SerializedObject(catalog);
            var property = serialized.GetIterator();
            var checkedCount = 0;
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script") continue;
                var sheet = property.objectReferenceValue as StyleSheet;
                Assert.IsNotNull(sheet, property.name);
                Assert.IsNotEmpty(AssetDatabase.GetAssetPath(sheet), property.name);
                checkedCount++;
            }
            Assert.Greater(checkedCount, 0);
        }

        [Test]
        public void EmptyCatalogListsEveryMissingName()
        {
            var catalog = ScriptableObject.CreateInstance<UiAssetCatalog>();
            var exception = Assert.Throws<InvalidOperationException>(catalog.ValidateAllAssigned);
            StringAssert.Contains("gamePanel, panelActionButton, modeSwitch, toast", exception.Message);
            UnityEngine.Object.DestroyImmediate(catalog);
        }
    }
}
