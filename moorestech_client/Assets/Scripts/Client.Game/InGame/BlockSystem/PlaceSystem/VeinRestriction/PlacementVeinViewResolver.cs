using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Map.MapVein;
using Core.Master;
using Game.Block.Interface.Vein;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction
{
    /// <summary>
    ///     設置対象と鉱脈限定の状態から、鉱脈範囲表示へ渡す鉱脈の集合を決めてプッシュする
    ///     Decides which veins the vein range view should draw from the placement target and the restriction, and pushes them
    /// </summary>
    public static class PlacementVeinViewResolver
    {
        public static void PushToView(IMapVeinRangeView mapVeinRangeView, MapVeinAabbRegistry veinAabbRegistry, VeinRestrictedPlacementState veinRestrictedPlacementState, IPlacementTarget target)
        {
            mapVeinRangeView.SetVeinDisplay(Resolve(veinAabbRegistry, veinRestrictedPlacementState, target));
        }

        /// <summary>
        ///     制限対象ブロックを持っている間はその種別の鉱脈を、採掘機は実際に掘れる鉱脈だけを、ポンプは流体鉱脈を出す
        ///     While the restricted block is held every vein of its type shows; a miner shows only the veins it can actually mine, a pump shows fluid veins
        /// </summary>
        public static VeinDisplay Resolve(MapVeinAabbRegistry veinAabbRegistry, VeinRestrictedPlacementState veinRestrictedPlacementState, IPlacementTarget target)
        {
            if (target is not BlockPlacementTarget blockTarget) return VeinDisplay.Hidden;

            var blockId = MasterHolder.BlockMaster.GetBlockId(blockTarget.BlockGuid);
            if (veinRestrictedPlacementState.TryGetRestrictedVeinType(blockId, out var restrictedVeinTypeGuid)) return VeinDisplay.OfVeins(veinAabbRegistry.SelectVeinsOfType(restrictedVeinTypeGuid), true);

            var blockParam = MasterHolder.BlockMaster.GetBlockMaster(blockTarget.BlockGuid).BlockParam;
            return blockParam switch
            {
                IMinerParam minerParam => VeinDisplay.OfVeins(SelectMinableVeins(minerParam), false),
                IPumpParam pumpParam => VeinDisplay.OfVeins(SelectPumpableVeins(pumpParam), false),
                _ => VeinDisplay.Hidden,
            };

            #region Internal

            // 表示は位置に依らないので掘れるアイテム種別だけで絞る。XZ重なりは設置判定側が同じ鉱脈集合に対して見る
            // The display does not depend on position, so filter by minable item only; the placement check applies the XZ overlap to the same set
            List<MapVeinAabb> SelectMinableVeins(IMinerParam minerParam)
            {
                var minableItemIds = MinerVeinFootprintJudge.ResolveMinableItemIds(minerParam.MineSettings);
                var veins = new List<MapVeinAabb>();
                foreach (var vein in veinAabbRegistry.Veins)
                    if (vein.VeinItemId.HasValue && minableItemIds.Contains(vein.VeinItemId.Value))
                        veins.Add(vein);

                return veins;
            }

            // ポンプも同じ構図。汲み上げられる流体の鉱脈だけを出し、設置判定と同じ集合にする（ADR 0051）
            // Pumps follow the same shape: show only veins of pumpable fluids, the same set the placement check uses (ADR 0051)
            List<MapVeinAabb> SelectPumpableVeins(IPumpParam pumpParam)
            {
                var pumpableFluidIds = PumpVeinFootprintJudge.ResolvePumpableFluidIds(pumpParam.GenerateFluid);
                var veins = new List<MapVeinAabb>();
                foreach (var vein in veinAabbRegistry.Veins)
                    if (vein.VeinFluidId.HasValue && pumpableFluidIds.Contains(vein.VeinFluidId.Value))
                        veins.Add(vein);

                return veins;
            }

            #endregion
        }
    }
}
