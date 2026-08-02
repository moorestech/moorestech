// [uGUI廃止Phase1] Web UI移行済みのため未メンテ・描画恒久停止。Phase2で削除予定（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] Unmaintained; rendering permanently disabled after the Web UI migration. Slated for deletion in Phase2 (docs/webui/ugui-retirement-plan.md)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Context;
using Client.Mod.Texture;
using Common.Debug;
using Core.Master;
using Game.UnlockState;

namespace Client.Game.InGame.UI.BuildMenu
{
    /// <summary>
    /// ビルドメニューの表示エントリ一覧を組み立てる（共有カタログの列挙順にアイコンとツールチップを付ける）
    /// Builds the list of build-menu entries by decorating the shared catalog's enumeration with icons and tooltips
    /// </summary>
    public static class BuildMenuEntryCatalog
    {
        public static List<BuildMenuEntry> CreateEntries(IGameUnlockStateData unlockState, PlacementTargetCatalog placementTargetCatalog, IReadOnlyList<(Guid id, string name)> blueprintEntries)
        {
            var entries = new List<BuildMenuEntry>();

            // 無料設置デバッグ時は未解放も含め設置可能な全ブロック/車両を表示する
            // In free-placement debug mode, show every placeable block/train car including locked ones
            var showAllPlaceable = DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement);

            // 共有カタログの列挙順（ブロック→車両→接続ツール→BPコピー→BP）がそのまま表示順
            // The shared catalog's order (blocks, train cars, connect tools, blueprint copy, blueprints) is the display order
            foreach (var entry in placementTargetCatalog.UnlockedEntries(unlockState, showAllPlaceable, blueprintEntries))
            {
                var target = PlacementTargetFactory.Create(entry);
                entries.Add(new BuildMenuEntry(target, ResolveIconView(target), CreateToolTip(target)));
            }

            return entries;

            #region Internal

            // アイコンを持つのはブロック・車両・接続ツールだけで、BPとBPコピーはテキスト表示スロット
            // Only blocks, train cars, and connect tools have icons; blueprints and the copy tool render as text-only slots
            ItemViewData ResolveIconView(IPlacementTarget target)
            {
                switch (target)
                {
                    case BlockPlacementTarget block:
                        return ClientContext.BlockImageContainer.GetBlockView(block.BlockId);
                    case TrainCarPlacementTarget trainCar:
                        return ClientContext.TrainCarImageContainer.GetTrainCarView(trainCar.TrainCarGuid);
                    case ConnectToolPlacementTarget connectTool:
                        return ClientContext.ConnectToolImageContainer.GetConnectToolView(connectTool.ConnectToolGuid);
                    default:
                        return null;
                }
            }

            // ツールチップは表示名に建設コストを続けたもの。コストを持つのはブロックと車両だけ
            // The tooltip is the display name followed by construction costs, which only blocks and train cars have
            string CreateToolTip(IPlacementTarget target)
            {
                var builder = new StringBuilder(target.DisplayName);
                switch (target)
                {
                    case BlockPlacementTarget block:
                        AppendRequiredItems(builder, MasterHolder.BlockMaster.GetBlockMaster(block.BlockId).RequiredItems?.Select(r => (r.ItemGuid, r.Count)));
                        break;
                    case TrainCarPlacementTarget trainCar:
                        AppendRequiredItems(builder, MasterHolder.TrainUnitMaster.GetTrainCarMaster(trainCar.TrainCarGuid).RequiredItems?.Select(r => (r.ItemGuid, r.Count)));
                        break;
                }
                return builder.ToString();
            }

            void AppendRequiredItems(StringBuilder builder, IEnumerable<(Guid itemGuid, int count)> requiredItems)
            {
                if (requiredItems == null) return;
                foreach (var (itemGuid, count) in requiredItems)
                {
                    builder.Append('\n').Append($"{MasterHolder.ItemMaster.GetItemMaster(itemGuid).Name} x{count}");
                }
            }

            #endregion
        }
    }
}
