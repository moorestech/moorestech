using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.Map.MapVein;
using Client.Game.InGame.UI.Tooltip;
using Game.Block.Interface;
using Game.Block.Interface.Vein;
using Mooresmaster.Localization.Generated;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    ///     採掘機の設置可否判定
    ///     - 底面と掘れるアイテム鉱脈のXZ重なりのみ判定
    ///     - クライアント側のみの制限
    ///     - サーバーは拒否しない
    ///     Miner placement gate
    ///     - XZ overlap of footprint and a minable item vein
    ///     - Client-side only
    ///     - Server does not reject
    /// </summary>
    public static class MinerVeinPlacementReporter
    {
        public static void MarkOutsideVeinCellsAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster, int cursorIndex, MapVeinAabbRegistry veinAabbRegistry, PlacementFeedback feedback)
        {
            // 採掘機以外は鉱脈と無関係なので素通しする
            // Anything but a miner is unrelated to veins, so let it pass
            if (holdingBlockMaster.BlockParam is not IMinerParam minerParam) return;

            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                var placeInfo = currentPlaceInfos[i];

                // サーバーと同じ判定で「置けるのに掘らない採掘機」を作らない。流体鉱脈はVeinItemIdが無いので掘れない
                // The same judge as the server so a placed miner always mines; fluid veins carry no VeinItemId and are never minable
                var footprint = new BlockPositionInfo(placeInfo.Position, placeInfo.Direction, holdingBlockMaster.BlockSize);
                if (OverlapsMinableVein(footprint)) continue;

                placeInfo.Placeable = false;
                if (i == cursorIndex) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceMinerOutsideVein));
            }

            #region Internal

            bool OverlapsMinableVein(BlockPositionInfo footprint)
            {
                foreach (var vein in veinAabbRegistry.Veins)
                {
                    if (vein.VeinItemId == null) continue;
                    if (!MinerVeinFootprintJudge.OverlapsXz(footprint, vein.MinCell, vein.MaxCell)) continue;
                    if (MinerVeinFootprintJudge.CanMine(minerParam.MineSettings, vein.VeinItemId.Value)) return true;
                }
                return false;
            }

            #endregion
        }
    }
}
