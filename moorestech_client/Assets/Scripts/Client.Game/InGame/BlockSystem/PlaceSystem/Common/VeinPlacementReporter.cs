using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.Map.MapVein;
using Client.Game.InGame.UI.Tooltip;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.Vein;
using Mooresmaster.Localization.Generated;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    ///     鉱脈に紐づく2つの設置制限を1箇所で課す。採掘機は掘れるアイテム鉱脈の上だけ、チュートリアル対象ブロックは指定種別の鉱脈の上だけ
    ///     Applies both vein-bound placement restrictions in one place: miners onto veins they can mine, tutorial-targeted blocks onto veins of the named type
    ///     位置の判定はサーバーと同じ BlockPositionInfoExtension.OverlapsVeinXz に委ねる（クライアント側のみの制限。サーバーは弾かない）
    ///     The positional test comes from the same BlockPositionInfoExtension.OverlapsVeinXz the server uses (client-side only; the server does not reject it)
    /// </summary>
    public static class VeinPlacementReporter
    {
        public static void MarkOutsideVeinCellsAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster, int cursorIndex, MapVeinAabbRegistry veinAabbRegistry, VeinRestrictedPlacementState state, PlacementFeedback feedback)
        {
            // 採掘機でもチュートリアル対象でもないブロックは鉱脈と無関係なので素通しする
            // A block that is neither a miner nor the tutorial target is unrelated to veins, so let it pass
            var minerParam = holdingBlockMaster.BlockParam as IMinerParam;
            var holdingBlockId = MasterHolder.BlockMaster.GetBlockId(holdingBlockMaster.BlockGuid);
            var isRestricted = state.TryGetRestrictedVeinType(holdingBlockId, out var restrictedVeinTypeGuid);
            if (minerParam == null && !isRestricted) return;

            // 掘れるアイテムIDの解決はセル数に依らず1度でよい。設置プレビューは毎フレーム回る
            // Resolving the minable item ids once is enough regardless of cell count; the placement preview runs every frame
            var minableItemIds = minerParam == null ? null : MinerVeinFootprintJudge.ResolveMinableItemIds(minerParam.MineSettings);

            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                var placeInfo = currentPlaceInfos[i];
                var footprint = new BlockPositionInfo(placeInfo.Position, placeInfo.Direction, holdingBlockMaster.BlockSize);

                // 2つの制限は同じ鉱脈台帳を見るので1度だけ回し、それぞれの重なりを同時に取る
                // Both restrictions read the same vein ledger, so one pass collects both overlaps at once
                var isOverMinableVein = false;
                var isOverRestrictedVeinType = false;
                foreach (var vein in veinAabbRegistry.Veins)
                {
                    if (minerParam != null && vein.VeinItemId.HasValue && MinerVeinFootprintJudge.IsMinableVein(footprint, minableItemIds, vein.MinCell, vein.MaxCell, vein.VeinItemId.Value)) isOverMinableVein = true;
                    if (isRestricted && vein.VeinTypeGuid == restrictedVeinTypeGuid && footprint.OverlapsVeinXz(vein.MinCell, vein.MaxCell)) isOverRestrictedVeinType = true;
                }

                // 置いた瞬間に何も掘らない採掘機を作らないため、掘れる鉱脈に重ならないセルは不可にする
                // A miner that would mine nothing the moment it lands is refused, so a cell overlapping no minable vein is not placeable
                if (minerParam != null && !isOverMinableVein)
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
