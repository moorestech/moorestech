using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.Map.MapVein;
using Client.Game.InGame.UI.Tooltip;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Mooresmaster.Localization.Generated;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    ///     鉱脈に紐づく2つの設置制限を1箇所で課す。採掘機はアイテム鉱脈の上だけ、チュートリアル対象ブロックは指定種別の鉱脈の上だけ
    ///     Applies both vein-bound placement restrictions in one place: miners onto item veins, tutorial-targeted blocks onto veins of the named type
    ///     判定セルの導出はサーバーと同じ BlockPositionInfoExtension.EnumerateVeinJudgeCells に委ねる（クライアント側のみの制限。サーバーは弾かない）
    ///     Judged cells come from the same BlockPositionInfoExtension.EnumerateVeinJudgeCells the server uses (client-side only; the server does not reject it)
    /// </summary>
    public static class VeinPlacementReporter
    {
        public static void MarkOutsideVeinCellsAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster, int cursorIndex, MapVeinAabbRegistry veinAabbRegistry, VeinRestrictedPlacementState state, PlacementFeedback feedback)
        {
            // 採掘機でもチュートリアル対象でもないブロックは鉱脈と無関係なので素通しする
            // A block that is neither a miner nor the tutorial target is unrelated to veins, so let it pass
            var isMiner = holdingBlockMaster.BlockParam is IMinerParam;
            var holdingBlockId = MasterHolder.BlockMaster.GetBlockId(holdingBlockMaster.BlockGuid);
            var isRestricted = state.TryGetRestrictedVeinType(holdingBlockId, out var restrictedVeinTypeGuid);
            if (!isMiner && !isRestricted) return;

            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                var placeInfo = currentPlaceInfos[i];
                var positionInfo = new BlockPositionInfo(placeInfo.Position, placeInfo.Direction, holdingBlockMaster.BlockSize);

                // 判定セルは2つの制限で共通なので1度だけ回し、それぞれの内包を同時に取る
                // Both restrictions judge the same cells, so one pass collects both containments at once
                var isOverItemVein = false;
                var isOverRestrictedVeinType = false;
                foreach (var cell in positionInfo.EnumerateVeinJudgeCells(holdingBlockMaster))
                {
                    if (isMiner && veinAabbRegistry.IsInsideAnyVeinOfKind(cell, MapVeinKind.Item)) isOverItemVein = true;
                    if (isRestricted && veinAabbRegistry.IsInsideAnyVeinOfType(cell, restrictedVeinTypeGuid)) isOverRestrictedVeinType = true;
                }

                // 採掘機が実際に掘れるのはアイテム鉱脈だけなので、流体鉱脈の上は設置可にしない
                // A miner can only ever mine item veins, so a fluid vein must not make the cell placeable
                if (isMiner && !isOverItemVein)
                {
                    placeInfo.Placeable = false;
                    if (i == cursorIndex) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceMinerOutsideVein));
                }

                if (isRestricted && !isOverRestrictedVeinType)
                {
                    placeInfo.Placeable = false;
                    if (i == cursorIndex) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceOutsideTutorialVein));
                }
            }
        }
    }
}
