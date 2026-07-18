using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Client.Tests.Removal
{
    public class CraftTreeRemovalTest
    {
        private const string InventoryPrefabPath = "Assets/Asset/UI/Prefab/Inventory/InventoryItems.prefab";

        [Test]
        public void InventoryPrefabDoesNotContainCraftTreeUi()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InventoryPrefabPath);
            Assert.IsNotNull(prefab, InventoryPrefabPath);

            // 旧ルート名と表示文言で残骸を検知
            // Detect remnants by legacy root names and visible text
            var obsoleteObjects = prefab.GetComponentsInChildren<Transform>(true)
                .Where(IsObsoleteCraftTreeObject)
                .Select(BuildHierarchyPath)
                .ToArray();

            Assert.IsEmpty(obsoleteObjects, string.Join(Environment.NewLine, obsoleteObjects));
        }

        private static bool IsObsoleteCraftTreeObject(Transform target)
        {
            if (target.name == "CraftTree" || target.name == "RecipeTreeView" || target.name == "show craft tree")
            {
                return true;
            }

            return target.GetComponents<Component>()
                .Where(component => component != null)
                .Select(component => new SerializedObject(component).FindProperty("m_text"))
                .Any(textProperty => textProperty != null &&
                                     textProperty.stringValue.Contains("クラフトツリー", StringComparison.Ordinal));
        }

        private static string BuildHierarchyPath(Transform target)
        {
            var pathSegments = target.GetComponentsInParent<Transform>(true)
                .Reverse()
                .Select(transform => transform.name);
            return string.Join("/", pathSegments);
        }
    }
}
