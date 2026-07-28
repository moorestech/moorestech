using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Context;
using Common.Debug;
using Client.Mod.Texture;
using Core.Master;
using Game.PlacementTarget;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.TrainModule;

namespace Client.Game.InGame.UI.BuildMenu
{
    /// <summary>
    /// ビルドメニューの表示エントリ一覧を組み立てる（共有カタログの列挙順にアイコンとツールチップを付ける）
    /// Builds the list of build-menu entries by decorating the shared catalog's enumeration with icons and tooltips
    /// </summary>
    public static class BuildMenuEntryCatalog
    {
        public static List<BuildMenuEntry> CreateEntries(IGameUnlockStateData unlockState, ClientBlueprintLibrary blueprintLibrary)
        {
            var entries = new List<BuildMenuEntry>();

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
                    default:
                        // ビルドツールとBPは解放条件を持たず常に表示する
                        // Build tools and blueprints have no unlock condition and are always shown
                        return true;
                }
            }

            BuildMenuEntry CreateEntry(PlacementTargetEntry entry, IPlacementTarget target)
            {
                switch (entry.Kind)
                {
                    case PlacementTargetKind.Block:
                    {
                        var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(entry.Id);
                        var iconView = ClientContext.BlockImageContainer.GetBlockView(MasterHolder.BlockMaster.GetBlockId(entry.Id));
                        return new BuildMenuEntry(target, iconView, CreateBlockToolTip(blockMaster));
                    }
                    case PlacementTargetKind.TrainCar:
                    {
                        // カタログのGuidは車両マスタ由来のため必ず引ける
                        // The catalog's guid always originates from the train car master, so this lookup always succeeds
                        MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(entry.Id, out var trainCar);
                        var iconView = ClientContext.TrainCarImageContainer.GetTrainCarView(entry.Id);
                        return new BuildMenuEntry(target, iconView, CreateTrainCarToolTip(trainCar, iconView));
                    }
                    case PlacementTargetKind.ConnectTool:
                    {
                        // 接続ツールのアイコンはconnectToolのimagePath由来
                        // The connect tool icon comes from the connectTool's imagePath
                        var iconView = ClientContext.ConnectToolImageContainer.GetConnectToolView(entry.Id);
                        return new BuildMenuEntry(target, iconView, entry.DisplayName);
                    }
                    default:
                        // ビルドツールとBPはアイコン無し（テキスト表示スロット）
                        // Build tools and blueprints have no icon and render as text-only slots
                        return new BuildMenuEntry(target, null, entry.DisplayName);
                }
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
