using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.ConnectTool;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Context;
using Game.Block.Interface.Extension;
using Client.Mod.Texture;
using Core.Master;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.TrainModule;

namespace Client.Game.InGame.UI.BuildMenu
{
    /// <summary>
    /// ビルドメニューの表示エントリ一覧を組み立てる（ブロック→車両→接続ツール→BPの順）
    /// Builds the list of build-menu entries: blocks, train cars, connect tools, then blueprints
    /// </summary>
    public static class BuildMenuEntryCatalog
    {
        public static List<BuildMenuEntry> CreateEntries(IGameUnlockStateData unlockState, ClientBlueprintLibrary blueprintLibrary)
        {
            var entries = new List<BuildMenuEntry>();

            // 解放済みブロックをソート順に列挙する（ベルト隠しバリアントは除外）
            // Enumerate unlocked blocks in sort order (exclude hidden belt variants)
            var unlockedBlocks = MasterHolder.BlockMaster.Blocks.Data
                .Where(b => IsBlockUnlocked(unlockState, b))
                .Where(b => !IsHiddenBeltConveyorVariant(b.BlockGuid))
                .OrderBy(b => b.SortPriority ?? 0)
                .ThenBy(b => b.Name);
            foreach (var blockMaster in unlockedBlocks)
            {
                var blockId = MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid);
                var iconView = ClientContext.BlockImageContainer.GetBlockView(blockId);
                entries.Add(new BuildMenuEntry(new BlockPlacementTarget(blockId, null), iconView, CreateBlockToolTip(blockMaster)));
            }

            // 解放済み車両を列挙する
            // Enumerate unlocked train cars
            foreach (var trainCar in MasterHolder.TrainUnitMaster.Train.TrainCars)
            {
                if (!unlockState.TrainCarUnlockStateInfos.TryGetValue(trainCar.TrainCarGuid, out var state) || !state.IsUnlocked) continue;
                var iconView = ClientContext.TrainCarImageContainer.GetTrainCarView(trainCar.TrainCarGuid);
                entries.Add(new BuildMenuEntry(new TrainCarPlacementTarget(trainCar.TrainCarGuid), iconView, CreateTrainCarToolTip(trainCar, iconView)));
            }

            // 接続ツールはコード定義のカタログから常時表示する（アイコンは敷設素材アイテム）
            // Connect tools always come from the code-defined catalog (the icon is the laying-material item)
            foreach (var tool in ConnectToolCatalog.GetDefinitionsInDisplayOrder())
            {
                var iconItemGuid = tool.SelectIconItemGuid();
                var iconView = iconItemGuid == null ? null : ClientContext.ItemImageContainer.GetItemView(iconItemGuid.Value);
                entries.Add(new BuildMenuEntry(new ConnectToolPlacementTarget(tool.ToolType), iconView, tool.DisplayName));
            }

            // 接続ツール群にBPコピーツール追加（テキスト表示）
            // Append the blueprint copy tool alongside the connect tools (icon-less text slot)
            entries.Add(new BuildMenuEntry(new BlueprintCopyToolPlacementTarget(), null, "ブループリントコピー"));

            // 保存済みBPのエントリを追加
            // Append entries for saved blueprints
            foreach (var blueprint in blueprintLibrary.Blueprints)
            {
                entries.Add(new BuildMenuEntry(new BlueprintPlacementTarget(blueprint.Name), null, blueprint.Name));
            }

            return entries;

            #region Internal

            bool IsBlockUnlocked(IGameUnlockStateData state, BlockMasterElement blockMaster)
            {
                return state.BlockUnlockStateInfos.TryGetValue(blockMaster.BlockGuid, out var info) && info.IsUnlocked;
            }

            // ベルトの長尺・斜面バリアントはメニューに出さず、代表ブロックだけを見せる
            // Hide belt length/slope variants from the menu and show only the representative block
            bool IsHiddenBeltConveyorVariant(Guid blockGuid)
            {
                var blockId = MasterHolder.BlockMaster.GetBlockId(blockGuid);
                return BeltConveyorPlaceFamilyUtil.TryGetFamilyByGuid(blockGuid, out var family) && family.IsHiddenVariant(blockId);
            }

            string CreateBlockToolTip(BlockMasterElement blockMaster)
            {
                var builder = new StringBuilder(blockMaster.Name);
                AppendRequiredItems(builder, ConstructionCostTexts(blockMaster.RequiredItems?.Select(r => (r.ItemGuid, r.Count))));
                return builder.ToString();
            }

            string CreateTrainCarToolTip(TrainCarMasterElement trainCar, ItemViewData iconView)
            {
                // 車両マスタにnameが無いため、アイコンビューの表示名（addressablePath末尾）を使う
                // Train car masters have no name, so use the icon view's display name (addressablePath tail)
                var builder = new StringBuilder(iconView.ItemName);
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
