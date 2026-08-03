using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.Tooltip;
using Client.Mod.Texture;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Client.Tests.Localization
{
    public class ItemSlotDefaultTooltipTest
    {
        private const string ItemSlotPrefabPath = "Assets/AddressableResources/UI/ItemSlotView.prefab";

        [Test]
        public void ItemMasterを持たない既定Tooltipは原文名を返さない()
        {
            var texture = new Texture2D(1, 1);
            var itemView = new ItemViewData(texture, "Source-only train");

            Assert.That(ItemSlotView.GetToolTipText(itemView), Is.Empty);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void 再利用スロットを非Master既定表示へ変えると古いTooltipを消す()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemSlotPrefabPath);
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            var slot = instance.GetComponent<ItemSlotView>();
            var tooltipTarget = instance.GetComponentInChildren<UGuiTooltipTarget>(true);
            var texture = new Texture2D(1, 1);
            var itemView = new ItemViewData(texture, "Source-only train");

            slot.SetItem(itemView, 0, "Explicit localized tooltip");
            AssertTooltipState(tooltipTarget, true, "Explicit localized tooltip");

            slot.SetItem(itemView, 0);

            AssertTooltipState(tooltipTarget, false, string.Empty);

            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(texture);
        }

        // 退役uGUIスロットのtooltipは通しキー＋パラメータ形なので、表示文字列はパラメータ側を検証する
        // Retired uGUI slot tooltips use a pass-through key plus params, so the display text is asserted on the param
        private static void AssertTooltipState(
            UGuiTooltipTarget tooltipTarget,
            bool expectedDisplay,
            string expectedText)
        {
            var serializedTarget = new SerializedObject(tooltipTarget);
            Assert.That(
                serializedTarget.FindProperty("displayEnable").boolValue,
                Is.EqualTo(expectedDisplay));
            Assert.That(
                serializedTarget.FindProperty("textKey").stringValue,
                Is.EqualTo(LocalizationKeys.Ui.Tooltip.RetiredUguiSlotText.Key));

            var textParams = serializedTarget.FindProperty("textParams");
            Assert.That(textParams.arraySize, Is.EqualTo(1));
            Assert.That(
                textParams.GetArrayElementAtIndex(0).stringValue,
                Is.EqualTo(expectedText));
        }
    }
}
