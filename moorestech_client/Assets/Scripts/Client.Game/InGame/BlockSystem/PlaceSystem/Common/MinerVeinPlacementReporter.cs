using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.Map.MapVein;
using Client.Game.InGame.UI.Tooltip;
using Game.Block.Interface;
using Mooresmaster.Localization.Generated;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    ///     採掘機は底面がアイテム鉱脈とXZで重なるセルにしか置けないようにする（クライアント側の設置制限。サーバーは弾かない）
    ///     Restricts miners to cells where the footprint overlaps an item vein in XZ (client-side only; the server does not reject it)
    /// </summary>
    public static class MinerVeinPlacementReporter
    {
        public static void MarkOutsideVeinCellsAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster, int cursorIndex, MapVeinAabbRegistry veinAabbRegistry, PlacementFeedback feedback)
        {
            // 採掘機以外は鉱脈と無関係なので素通しする
            // Anything but a miner is unrelated to veins, so let it pass
            if (holdingBlockMaster.BlockParam is not IMinerParam) return;

            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                var placeInfo = currentPlaceInfos[i];

                // 採掘機が実際に掘れるのはアイテム鉱脈だけなので、流体鉱脈の上は設置可にしない
                // A miner can only ever mine item veins, so a fluid vein must not make the cell placeable
                var footprint = new BlockPositionInfo(placeInfo.Position, placeInfo.Direction, holdingBlockMaster.BlockSize);
                if (veinAabbRegistry.IsOverlappingFootprint(footprint, MapVeinKind.Item)) continue;

                placeInfo.Placeable = false;
                if (i == cursorIndex) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceMinerOutsideVein));
            }
        }
    }
}
