using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Logic;
using Client.Ui.State.PlacementTarget;
using Client.Ui.State.Text;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.State.BuildMenu.Source
{
    /// <summary>
    /// 配置対象の列 → エントリの列（Web の橋 BuildMenuEntryDtoFactory.CreateDtos の写し）
    /// Placement targets to entries (port of the Web bridge BuildMenuEntryDtoFactory.CreateDtos)
    /// </summary>
    public static class BuildMenuEntryFactory
    {
        public static IReadOnlyList<BuildMenuEntryViewModel> CreateEntries(IReadOnlyList<IPlacementTarget> targets, ConstructionWalletQuery walletQuery, IEnumerable<IItemStack> inventoryItems, IBlueprintThumbnailLookup thumbnails)
        {
            var heldByItem = ConstructionMaterialAccounting.TallyHeld(inventoryItems);
            var freeBlockPlacement = DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement);
            var entries = new List<BuildMenuEntryViewModel>();
            foreach (var target in targets) entries.Add(CreateEntry(target));
            return entries;

            #region Internal

            BuildMenuEntryViewModel CreateEntry(IPlacementTarget target)
            {
                // 財布はブロックだけが持つ。免除は無料設置・BP の免除・財布の残りのどれか
                // Only blocks have a wallet; waived by free placement, a waived blueprint, or wallet stock
                var (categoryGuid, subCategoryGuid) = BuildMenuCategoryPairResolver.Resolve(target);
                var block = target as BlockPlacementTarget;
                var walletStatus = block == null ? null : walletQuery.GetWalletStatus(block.BlockId);
                var (rows, blueprintWaived) = target is BlueprintPlacementTarget blueprint
                    ? BuildMenuMaterialRowsBuilder.ForBlueprint(blueprint, walletQuery, freeBlockPlacement, heldByItem)
                    : (BuildMenuMaterialRowsBuilder.ForTarget(target, heldByItem), false);
                var paymentWaived = (freeBlockPlacement && block != null) || blueprintWaived || (walletStatus?.CoversNextPlacement() ?? false);

                // 1 セット設置は Block の財布の状態からだけ作る
                // A set placement only comes from a block's wallet status
                BuildMenuSetPlacement setPlacement = walletStatus is { } status ? new BuildMenuSetPlacementAvailable(status.PlacementsPerCost, status.RemainingCount) : BuildMenuSetPlacementNone.Instance;
                var icon = PlacementTargetIconResolver.Resolve(target, thumbnails);

                // 表示名はマスタ由来で空にならない前提。空なら開発者へ知らせ、表示は Web と同じくそのまま（前例 ModeHudViewModelFactory）
                // Display names come from the master and should never be empty; tell developers and display as is, like Web (precedent ModeHudViewModelFactory)
                if (target.DisplayName.Length == 0) Debug.LogError($"[BuildMenu] empty DisplayName for placement target {target.Kind} {target.Id}");
                return new BuildMenuEntryViewModel(target.Id, target.Kind, categoryGuid, subCategoryGuid, target.DisplayName, rows, paymentWaived, icon, setPlacement);
            }

            #endregion
        }

        public static IReadOnlyList<BuildMenuCategory> CreateCategories()
        {
            return MasterHolder.BuildMenuCategoryMaster.Categories
                .Select(category => new BuildMenuCategory(category.CategoryGuid, category.SubCategories.Select(sub => sub.SubCategoryGuid).ToList()))
                .ToList();
        }

        // 見出しの名前はコンテンツ辞書キー（Web の buildMenuCategoryNameKey・buildMenuSubCategoryNameKey）
        // Heading names are content keys (the Web buildMenuCategoryNameKey and buildMenuSubCategoryNameKey)
        public static IReadOnlyDictionary<Guid, UiText> CreateHeadingNames()
        {
            var names = new Dictionary<Guid, UiText>();
            foreach (var category in MasterHolder.BuildMenuCategoryMaster.Categories)
            {
                names[category.CategoryGuid] = UiText.FromContent(ContentLocalizationKeys.BuildMenuCategoryName(category.CategoryGuid));
                foreach (var sub in category.SubCategories) names[sub.SubCategoryGuid] = UiText.FromContent(ContentLocalizationKeys.BuildMenuSubCategoryName(sub.SubCategoryGuid));
            }
            return names;
        }
    }
}
