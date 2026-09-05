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
    ///     鉱脈に紐づく3つの設置制限を1箇所で課す。採掘機は掘れるアイテム鉱脈の上だけ、ポンプは汲み上げられる流体鉱脈の上だけ、チュートリアル対象ブロックは指定種別の鉱脈の上だけ
    ///     Applies all three vein-bound placement restrictions in one place: miners onto veins they can mine, pumps onto veins they can draw from, tutorial-targeted blocks onto veins of the named type
    ///     位置の判定はサーバーと同じ BlockPositionInfoExtension.OverlapsVeinXz に委ねる（クライアント側のみの制限。サーバーは弾かない）
    ///     The positional test comes from the same BlockPositionInfoExtension.OverlapsVeinXz the server uses (client-side only; the server does not reject it)
    /// </summary>
    public static class VeinPlacementReporter
    {
        public static void MarkOutsideVeinCellsAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster, int cursorIndex, MapVeinAabbRegistry veinAabbRegistry, VeinRestrictedPlacementState state, PlacementFeedback feedback)
        {
            // 採掘機・ポンプ・チュートリアル対象のいずれでもないブロックは鉱脈と無関係なので素通しする
            // A block that is neither a miner, a pump nor the tutorial target is unrelated to veins, so let it pass
            var minerParam = holdingBlockMaster.BlockParam as IMinerParam;
            var pumpParam = holdingBlockMaster.BlockParam as IPumpParam;
            var holdingBlockId = MasterHolder.BlockMaster.GetBlockId(holdingBlockMaster.BlockGuid);
            var isRestricted = state.TryGetRestrictedVeinType(holdingBlockId, out var restrictedVeinTypeGuid);
            if (minerParam == null && pumpParam == null && !isRestricted) return;

            // 掘れる/汲み上げられるIDの解決はセル数に依らず1度でよい。設置プレビューは毎フレーム回る
            // Resolving the minable / pumpable ids once is enough regardless of cell count; the placement preview runs every frame
            var minableItemIds = minerParam == null ? null : MinerVeinFootprintJudge.ResolveMinableItemIds(minerParam.MineSettings);
            var pumpableFluidIds = pumpParam == null ? null : PumpVeinFootprintJudge.ResolvePumpableFluidIds(pumpParam.GenerateFluid);

            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                var placeInfo = currentPlaceInfos[i];
                var footprint = new BlockPositionInfo(placeInfo.Position, placeInfo.Direction, holdingBlockMaster.BlockSize);

                // 3つの制限は同じ鉱脈台帳を見るので1度だけ回し、それぞれの重なりを同時に取る
                // All three restrictions read the same vein ledger, so one pass collects every overlap at once
                var isOverMinableVein = false;
                var isOverPumpableVein = false;
                var isOverRestrictedVeinType = false;
                foreach (var vein in veinAabbRegistry.Veins)
                {
                    if (minerParam != null && vein.VeinItemId.HasValue && MinerVeinFootprintJudge.IsMinableVein(footprint, minableItemIds, vein.MinCell, vein.MaxCell, vein.VeinItemId.Value)) isOverMinableVein = true;
                    if (pumpParam != null && vein.VeinFluidId.HasValue && PumpVeinFootprintJudge.IsPumpableVein(footprint, pumpableFluidIds, vein.MinCell, vein.MaxCell, vein.VeinFluidId.Value)) isOverPumpableVein = true;
                    if (isRestricted && vein.VeinTypeGuid == restrictedVeinTypeGuid && footprint.OverlapsVeinXz(vein.MinCell, vein.MaxCell)) isOverRestrictedVeinType = true;
                }

                // 置いた瞬間に何も掘らない採掘機を作らないため、掘れる鉱脈に重ならないセルは不可にする
                // A miner that would mine nothing the moment it lands is refused, so a cell overlapping no minable vein is not placeable
                if (minerParam != null && !isOverMinableVein)
                {
                    placeInfo.Placeable = false;
                    if (i == cursorIndex) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceMinerOutsideVein));
                }

                // 置いた瞬間に何も汲み上げないポンプも同じ理由で不可にする（ADR 0051）
                // A pump that would draw nothing the moment it lands is refused for the same reason (ADR 0051)
                if (pumpParam != null && !isOverPumpableVein)
                {
                    placeInfo.Placeable = false;
                    if (i == cursorIndex) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlacePumpOutsideVein));
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
