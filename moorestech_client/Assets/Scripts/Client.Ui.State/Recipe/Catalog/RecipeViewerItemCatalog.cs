using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Core.Master;
using UnityEngine;

namespace Client.Ui.State.Recipe.Catalog
{
    /// <summary>
    /// 一覧に出すアイテムを決める（Web RecipeViewerItemListTopic の源）。未知の型は隠して警告を 1 回だけ出す
    /// Decides the items for the list (source of the Web RecipeViewerItemListTopic); unknown types are hidden with a single warning
    /// </summary>
    public sealed class RecipeViewerItemCatalog
    {
        private readonly ItemRecipeViewerDataContainer _viewerData;
        private bool _unknownTypeWarned;

        public RecipeViewerItemCatalog(ItemRecipeViewerDataContainer viewerData)
        {
            _viewerData = viewerData;
        }

        public IReadOnlyList<ItemId> BuildVisibleItems()
        {
            // マスタの列挙順を保って表示対象だけ詰める
            // Keep the master enumeration order and collect only visible items
            var result = new List<ItemId>();
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(itemId);
                var visibility = _viewerData.EvaluateVisibility(itemId, itemMaster);
                if (visibility == RecipeViewerItemVisibility.UnknownType) WarnUnknownTypeOnce(itemMaster.RecipeViewType);
                if (visibility == RecipeViewerItemVisibility.Show) result.Add(itemId);
            }
            return result;

            #region Internal

            void WarnUnknownTypeOnce(string recipeViewType)
            {
                // 未知の型は隠す（fail-closed）。開発者が気づくよう最初の 1 回だけ警告する
                // Unknown types are hidden (fail-closed); warn once so developers notice
                if (_unknownTypeWarned) return;
                _unknownTypeWarned = true;
                Debug.LogWarning($"[RecipeViewerItemCatalog] 未知の RecipeViewType のアイテムを一覧から隠した: {recipeViewType}");
            }

            #endregion
        }
    }
}
