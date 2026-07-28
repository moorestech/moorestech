using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Context;
using Common.Debug;
using Core.Master;
using Game.PlacementTarget;
using Game.UnlockState;
using Mooresmaster.Model.BuildMenuModule;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// web建設メニューのエントリ一覧を組み立てる。非ブロックのカテゴリはbuildMenuマスタのentrySourceから導出する
    /// Builds the web build-menu entries. Non-block categories are derived from the buildMenu master's entrySource
    /// </summary>
    public static class WebBuildMenuEntryCatalog
    {
        public static List<WebBuildMenuEntry> CreateEntries(IGameUnlockStateData unlockState, ClientBlueprintLibrary blueprintLibrary)
        {
            var entries = new List<WebBuildMenuEntry>();
            var categoryMaster = MasterHolder.BuildMenuCategoryMaster;

            // 無料設置デバッグ時は未解放も含め設置可能な全ブロック/車両を表示する
            // In free-placement debug mode, show every placeable block/train car including locked ones
            var showAllPlaceable = DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement);

            // 共有カタログの列挙順（ブロック→車両→接続ツール→ビルドツール→BP）がそのまま表示順
            // The shared catalog's order (blocks, train cars, connect tools, build tools, blueprints) is the display order
            foreach (var entry in new PlacementTargetCatalog(blueprintLibrary).Entries)
            {
                if (!IsUnlocked(entry)) continue;
                if (!PlacementTargetFactory.TryCreate(entry, out var target)) continue;
                entries.Add(CreateEntry(entry, target));
            }

            return entries;

            #region Internal

            bool IsUnlocked(PlacementTargetEntry entry)
            {
                switch (entry.Kind)
                {
                    case PlacementTargetKind.Block:
                        return showAllPlaceable || (unlockState.BlockUnlockStateInfos.TryGetValue(entry.Id, out var blockInfo) && blockInfo.IsUnlocked);
                    case PlacementTargetKind.TrainCar:
                        return showAllPlaceable || (unlockState.TrainCarUnlockStateInfos.TryGetValue(entry.Id, out var trainCarInfo) && trainCarInfo.IsUnlocked);
                    case PlacementTargetKind.ConnectTool:
                        return unlockState.ConnectToolUnlockStateInfos.TryGetValue(entry.Id, out var connectToolInfo) && connectToolInfo.IsUnlocked;
                    case PlacementTargetKind.BuildTool:
                    case PlacementTargetKind.Blueprint:
                        // ビルドツールとBPは解放条件を持たず常に表示する
                        // Build tools and blueprints have no unlock condition and are always shown
                        return true;
                    default:
                        // 未知のKindは型で排除する到達不能ケース
                        // Unreachable: unknown Kind is excluded by the type
                        throw new ArgumentOutOfRangeException();
                }
            }

            WebBuildMenuEntry CreateEntry(PlacementTargetEntry entry, IPlacementTarget target)
            {
                switch (entry.Kind)
                {
                    case PlacementTargetKind.Block:
                    {
                        // ブロックだけはカテゴリをブロックマスタ自身が持つ
                        // Only blocks carry their category on the block master itself
                        var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(entry.Id);
                        var requiredItems = ToRequiredItems(blockMaster.RequiredItems?.Select(r => (r.ItemGuid, r.Count)));
                        return new WebBuildMenuEntry(target, blockMaster.Name, blockMaster.Category, blockMaster.SubCategory, requiredItems);
                    }
                    case PlacementTargetKind.TrainCar:
                    {
                        // カタログのGuidは車両マスタ由来のため必ず引ける
                        // The catalog's guid always originates from the train car master, so this lookup always succeeds
                        MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(entry.Id, out var trainCar);

                        // 車両マスタにnameが無いため、アイコンビューの表示名（addressablePath末尾）を使う
                        // Train car masters have no name, so use the icon view's display name (addressablePath tail)
                        var iconView = ClientContext.TrainCarImageContainer.GetTrainCarView(entry.Id);
                        var (category, subCategory) = categoryMaster.GetPairByEntrySource(BuildMenuSubCategoryElement.EntrySourceConst.trainCars);
                        return new WebBuildMenuEntry(target, iconView.ItemName, category, subCategory, ToRequiredItems(trainCar.RequiredItems?.Select(r => (r.ItemGuid, r.Count))));
                    }
                    case PlacementTargetKind.ConnectTool:
                        return CreateCostlessEntry(entry, target, BuildMenuSubCategoryElement.EntrySourceConst.connectTools);
                    case PlacementTargetKind.BuildTool:
                        return CreateCostlessEntry(entry, target, BuildMenuSubCategoryElement.EntrySourceConst.blueprintCopyTool);
                    case PlacementTargetKind.Blueprint:
                        return CreateCostlessEntry(entry, target, BuildMenuSubCategoryElement.EntrySourceConst.savedBlueprints);
                    default:
                        // 未知のKindは型で排除する到達不能ケース
                        // Unreachable: unknown Kind is excluded by the type
                        throw new ArgumentOutOfRangeException();
                }
            }

            WebBuildMenuEntry CreateCostlessEntry(PlacementTargetEntry entry, IPlacementTarget target, string entrySource)
            {
                // 建設コストを持たない種別はentrySource定義のサブカテゴリへ入れるだけ
                // Kinds without construction costs merely go to their entrySource-defined sub category
                var (category, subCategory) = categoryMaster.GetPairByEntrySource(entrySource);
                return new WebBuildMenuEntry(target, entry.DisplayName, category, subCategory, new List<WebBuildMenuEntry.RequiredItem>());
            }

            List<WebBuildMenuEntry.RequiredItem> ToRequiredItems(IEnumerable<(Guid itemGuid, int count)> requiredItems)
            {
                // ItemGuidを揮発ItemIdへ解決
                // Resolve ItemGuid to a volatile ItemId
                var results = new List<WebBuildMenuEntry.RequiredItem>();
                if (requiredItems == null) return results;
                foreach (var (itemGuid, count) in requiredItems)
                {
                    results.Add(new WebBuildMenuEntry.RequiredItem(MasterHolder.ItemMaster.GetItemId(itemGuid), count));
                }
                return results;
            }

            #endregion
        }
    }
}
