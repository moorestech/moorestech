using System.Reflection;
using Client.Game.InGame.UI.Inventory.Common;
using UnityEngine;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    /// Electric/Gear共有の検証ヘルパー
    /// Shared verification helpers for the Electric/Gear machine recipe selection UI tests.
    /// </summary>
    internal static class MachineRecipeSelectionTestHelper
    {
        // 名前で子Transformを再帰検索する
        // Recursively find a child transform by name.
        public static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent.name == childName) return parent;
            foreach (Transform child in parent)
            {
                var found = FindChildRecursive(child, childName);
                if (found != null) return found;
            }
            return null;
        }

        // privateフィールドをリフレクションで取得
        // Walk private fields via reflection to read the highlight display state.
        public static bool IsHotBarSelected(ItemSlotView slot)
        {
            var commonSlotViewField = typeof(ItemSlotView).GetField("commonSlotView", BindingFlags.NonPublic | BindingFlags.Instance);
            var commonSlotView = (CommonSlotView)commonSlotViewField.GetValue(slot);
            var hotBarSelectField = typeof(CommonSlotView).GetField("hotBarSelect", BindingFlags.NonPublic | BindingFlags.Instance);
            var hotBarSelectObject = (GameObject)hotBarSelectField.GetValue(commonSlotView);
            return hotBarSelectObject.activeSelf;
        }
    }
}
