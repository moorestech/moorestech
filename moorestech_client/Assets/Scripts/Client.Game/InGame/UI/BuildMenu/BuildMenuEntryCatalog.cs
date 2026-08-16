// [uGUI廃止Phase1] Web UI移行済みのため未メンテ・描画恒久停止。Phase2で削除予定（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] Unmaintained; rendering permanently disabled after the Web UI migration. Slated for deletion in Phase2 (docs/webui/ugui-retirement-plan.md)
using System;
using System.Collections.Generic;
using System.Text;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Context;
using Client.Localization;
using Client.Mod.Texture;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.UI.BuildMenu
{
    /// <summary>
    /// 表示エントリへ装飾を付与
    /// Decorates display entries
    /// </summary>
    public static class BuildMenuEntryCatalog
    {
        public static List<BuildMenuEntry> CreateEntries(PlacementTargetResolver placementTargetResolver)
        {
            var entries = new List<BuildMenuEntry>();

            // 共有カタログの列挙順（ブロック→車両→接続ツール→BPコピー→BP）がそのまま表示順
            // The shared catalog's order (blocks, train cars, connect tools, blueprint copy, blueprints) is the display order
            foreach (var target in placementTargetResolver.CreateUnlockedTargets())
            {
                entries.Add(new BuildMenuEntry(target, ResolveIconView(target), CreateToolTip(target)));
            }

            return entries;

            #region Internal

            // 3種だけアイコン表示
            // Icons for three target kinds
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

            // ブロック・車両の建設費を追記
            // Appends block/train costs
            string CreateToolTip(IPlacementTarget target)
            {
                var builder = new StringBuilder(target.DisplayName);
                AppendRequiredItems(builder, target.CreateRequiredItems());
                return builder.ToString();
            }

            void AppendRequiredItems(StringBuilder builder, IEnumerable<(Guid itemGuid, int count)> requiredItems)
            {
                foreach (var (itemGuid, count) in requiredItems)
                {
                    builder.Append('\n').Append(
                        $"{Localize.GetContent(ContentLocalizationKeys.ItemName(itemGuid))} x{count}");
                }
            }

            #endregion
        }
    }
}
