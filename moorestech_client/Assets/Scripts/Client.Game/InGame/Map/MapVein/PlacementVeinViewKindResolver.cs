using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.Map.MapVein
{
    /// <summary>
    ///     設置対象と鉱脈限定の状態から、鉱脈範囲表示へ渡す表示状態を決めてプッシュする
    ///     Decides the display state the vein range view should show from the placement target and the restriction, and pushes it
    /// </summary>
    public static class PlacementVeinViewKindResolver
    {
        public static void PushToView(IMapVeinRangeView mapVeinRangeView, VeinRestrictedPlacementState veinRestrictedPlacementState, IPlacementTarget target)
        {
            mapVeinRangeView.SetVeinDisplay(Resolve(veinRestrictedPlacementState, target));
        }

        /// <summary>
        ///     制限対象ブロックを持っている間はその種別の鉱脈を、それ以外は設置が見たいkindを出す。採掘機はアイテム鉱脈、ポンプは流体鉱脈
        ///     While the restricted block is held every vein of its type shows; otherwise the kind the placement wants — item veins for miners, fluid veins for pumps
        /// </summary>
        public static VeinDisplay Resolve(VeinRestrictedPlacementState veinRestrictedPlacementState, IPlacementTarget target)
        {
            if (target is not BlockPlacementTarget blockTarget) return VeinDisplay.Hidden;

            var blockId = MasterHolder.BlockMaster.GetBlockId(blockTarget.BlockGuid);
            if (veinRestrictedPlacementState.TryGetRestrictedVeinType(blockId, out var restrictedVeinTypeGuid)) return VeinDisplay.OfVeinType(restrictedVeinTypeGuid);

            var blockParam = MasterHolder.BlockMaster.GetBlockMaster(blockTarget.BlockGuid).BlockParam;
            return blockParam switch
            {
                IMinerParam => VeinDisplay.OfKind(MapVeinKind.Item),
                GearPumpBlockParam or ElectricPumpBlockParam => VeinDisplay.OfKind(MapVeinKind.Fluid),
                _ => VeinDisplay.Hidden,
            };
        }
    }
}
