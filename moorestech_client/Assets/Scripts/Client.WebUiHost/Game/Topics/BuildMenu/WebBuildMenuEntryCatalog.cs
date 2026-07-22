using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.ConnectTool;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Context;
using Common.Debug;
using Core.Master;
using Game.Block.Interface.Extension;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;
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

            // 解放済み（無料設置時は全）ブロックをソート順に列挙し、ベルトの坂は除外する
            // Enumerate unlocked (all in free mode) blocks in sort order while excluding belt slopes
            var unlockedBlocks = MasterHolder.BlockMaster.Blocks.Data
                .Where(b => showAllPlaceable || IsBlockUnlocked(unlockState, b))
                .Where(b => !BeltConveyorPlaceFamilyUtil.IsSlopeBlock(b.BlockGuid))
                .OrderBy(b => b.SortPriority ?? 0)
                .ThenBy(b => b.Name);
            foreach (var blockMaster in unlockedBlocks)
            {
                var blockId = MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid);
                var requiredItems = ToRequiredItems(blockMaster.RequiredItems?.Select(r => (r.ItemGuid, r.Count)));
                entries.Add(new WebBuildMenuEntry(new BlockPlacementTarget(blockId, null), blockMaster.Name, blockMaster.Category, blockMaster.SubCategory, requiredItems));
            }

            // 解放済み車両を列挙する。行き先はentrySource:trainCarsのサブカテゴリ
            // Enumerate unlocked train cars; they go to the entrySource:trainCars sub category
            var (trainCarCategory, trainCarSubCategory) = categoryMaster.GetPairByEntrySource(BuildMenuSubCategoryElement.EntrySourceConst.trainCars);
            foreach (var trainCar in MasterHolder.TrainUnitMaster.Train.TrainCars)
            {
                if (!showAllPlaceable && (!unlockState.TrainCarUnlockStateInfos.TryGetValue(trainCar.TrainCarGuid, out var state) || !state.IsUnlocked)) continue;

                // 車両マスタにnameが無いため、アイコンビューの表示名（addressablePath末尾）を使う
                // Train car masters have no name, so use the icon view's display name (addressablePath tail)
                var iconView = ClientContext.TrainCarImageContainer.GetTrainCarView(trainCar.TrainCarGuid);
                var requiredItems = ToRequiredItems(trainCar.RequiredItems?.Select(r => (r.ItemGuid, r.Count)));
                entries.Add(new WebBuildMenuEntry(new TrainCarPlacementTarget(trainCar.TrainCarGuid), iconView.ItemName, trainCarCategory, trainCarSubCategory, requiredItems));
            }

            // 解放済みconnectToolをSortPriority順に列挙。行き先はentrySource:connectToolsのサブカテゴリ
            // Enumerate unlocked connectTools in SortPriority order; they go to the entrySource:connectTools sub category
            var (connectToolCategory, connectToolSubCategory) = categoryMaster.GetPairByEntrySource(BuildMenuSubCategoryElement.EntrySourceConst.connectTools);
            var unlockedConnectTools = MasterHolder.ConnectToolMaster.All
                .Where(element => unlockState.ConnectToolUnlockStateInfos.TryGetValue(element.ConnectToolGuid, out var info) && info.IsUnlocked)
                .OrderBy(element => element.SortPriority);
            foreach (var connectTool in unlockedConnectTools)
            {
                entries.Add(new WebBuildMenuEntry(new ConnectToolPlacementTarget(connectTool.ConnectToolGuid), connectTool.Name, connectToolCategory, connectToolSubCategory, new List<WebBuildMenuEntry.RequiredItem>()));
            }

            // BPコピーツールと保存済みBPもentrySource定義のサブカテゴリへ入れる
            // The blueprint copy tool and saved blueprints also go to their entrySource-defined sub categories
            var (copyToolCategory, copyToolSubCategory) = categoryMaster.GetPairByEntrySource(BuildMenuSubCategoryElement.EntrySourceConst.blueprintCopyTool);
            entries.Add(new WebBuildMenuEntry(new BlueprintCopyToolPlacementTarget(), "ブループリントコピー", copyToolCategory, copyToolSubCategory, new List<WebBuildMenuEntry.RequiredItem>()));

            var (blueprintCategory, blueprintSubCategory) = categoryMaster.GetPairByEntrySource(BuildMenuSubCategoryElement.EntrySourceConst.savedBlueprints);
            foreach (var blueprint in blueprintLibrary.Blueprints)
            {
                entries.Add(new WebBuildMenuEntry(new BlueprintPlacementTarget(blueprint.Name), blueprint.Name, blueprintCategory, blueprintSubCategory, new List<WebBuildMenuEntry.RequiredItem>()));
            }

            return entries;

            #region Internal

            bool IsBlockUnlocked(IGameUnlockStateData state, BlockMasterElement blockMaster)
            {
                return state.BlockUnlockStateInfos.TryGetValue(blockMaster.BlockGuid, out var info) && info.IsUnlocked;
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
