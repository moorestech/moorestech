using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Core.Master;
using Game.Block.Interface.Extension;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.PlaceSystemModule;
using Mooresmaster.Model.TrainModule;

namespace Client.Game.InGame.UI.BuildMenu
{
    /// <summary>
    /// ビルドメニューの表示エントリ一覧を組み立てる（ブロック→車両→接続ツールの順）
    /// Builds the list of build-menu entries: blocks, then train cars, then connect tools
    /// </summary>
    public static class BuildMenuEntryCatalog
    {
        public static List<BuildMenuEntry> CreateEntries(IGameUnlockStateData unlockState)
        {
            var entries = new List<BuildMenuEntry>();

            // 解放済みブロックをソート順に列挙する（ベルト隠しバリアントは除外）
            // Enumerate unlocked blocks in sort order (exclude hidden belt variants)
            var unlockedBlocks = MasterHolder.BlockMaster.Blocks.Data
                .Where(b => IsBlockUnlocked(unlockState, b))
                .Where(b => !BeltConveyorPlaceFamilyUtil.IsHiddenVariant(b.BlockGuid))
                .OrderBy(b => b.SortPriority ?? 0)
                .ThenBy(b => b.Name);
            foreach (var blockMaster in unlockedBlocks)
            {
                var blockId = MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid);
                var iconItemId = MasterHolder.BlockMaster.GetItemId(blockId);
                entries.Add(new BuildMenuEntry(PlacementSelectionType.Block, blockId, default, null, iconItemId, CreateBlockToolTip(blockMaster)));
            }

            // 解放済み車両を列挙する
            // Enumerate unlocked train cars
            foreach (var trainCar in MasterHolder.TrainUnitMaster.Train.TrainCars)
            {
                if (!unlockState.TrainCarUnlockStateInfos.TryGetValue(trainCar.TrainCarGuid, out var state) || !state.IsUnlocked) continue;
                var iconItemId = MasterHolder.ItemMaster.GetItemId(trainCar.ItemGuid);
                entries.Add(new BuildMenuEntry(PlacementSelectionType.TrainCar, default, trainCar.TrainCarGuid, null, iconItemId, CreateTrainCarToolTip(trainCar)));
            }

            // 接続ツールは常時表示する（ビルドメニュー対象外のBeltConveyorは除外）
            // Connect tools are always visible (BeltConveyor is not a build-menu entry, so skip it)
            var connectTools = MasterHolder.PlaceSystemMaster.PlaceSystem.Data
                .Where(e => e.PlaceMode != PlaceSystemMasterElement.PlaceModeConst.BeltConveyor)
                .OrderBy(e => e.SortPriority ?? 0);
            foreach (var tool in connectTools)
            {
                var iconItemId = MasterHolder.ItemMaster.GetItemId(tool.IconItemGuid.Value);
                entries.Add(new BuildMenuEntry(PlacementSelectionType.ConnectTool, default, default, tool.PlaceMode, iconItemId, tool.Name));
            }

            return entries;

            #region Internal

            bool IsBlockUnlocked(IGameUnlockStateData state, BlockMasterElement blockMaster)
            {
                return state.BlockUnlockStateInfos.TryGetValue(blockMaster.BlockGuid, out var info) && info.IsUnlocked;
            }

            string CreateBlockToolTip(BlockMasterElement blockMaster)
            {
                var builder = new StringBuilder(blockMaster.Name);
                AppendRequiredItems(builder, ConstructionCostTexts(blockMaster.RequiredItems?.Select(r => (r.ItemGuid, r.Count))));
                return builder.ToString();
            }

            string CreateTrainCarToolTip(TrainCarMasterElement trainCar)
            {
                var builder = new StringBuilder(MasterHolder.ItemMaster.GetItemMaster(trainCar.ItemGuid).Name);
                AppendRequiredItems(builder, ConstructionCostTexts(trainCar.RequiredItems?.Select(r => (r.ItemGuid, r.Count))));
                return builder.ToString();
            }

            IEnumerable<string> ConstructionCostTexts(IEnumerable<(Guid itemGuid, int count)> requiredItems)
            {
                if (requiredItems == null) yield break;
                foreach (var (itemGuid, count) in requiredItems)
                {
                    yield return $"{MasterHolder.ItemMaster.GetItemMaster(itemGuid).Name} x{count}";
                }
            }

            void AppendRequiredItems(StringBuilder builder, IEnumerable<string> costTexts)
            {
                foreach (var text in costTexts) builder.Append('\n').Append(text);
            }

            #endregion
        }
    }
}
