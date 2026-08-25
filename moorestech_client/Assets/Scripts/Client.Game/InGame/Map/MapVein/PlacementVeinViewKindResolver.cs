using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.Map.MapVein
{
    /// <summary>
    ///     設置対象から、その設置で見たい鉱脈種別を決める。採掘機はアイテム鉱脈、ポンプは流体鉱脈
    ///     Decides which vein kind a placement wants to see: miners want item veins, pumps want fluid veins
    /// </summary>
    public static class PlacementVeinViewKindResolver
    {
        public static MapVeinKind? Resolve(IPlacementTarget target)
        {
            if (target is not BlockPlacementTarget blockTarget) return null;

            var blockParam = MasterHolder.BlockMaster.GetBlockMaster(blockTarget.BlockGuid).BlockParam;
            return blockParam switch
            {
                IMinerParam => MapVeinKind.Item,
                GearPumpBlockParam or ElectricPumpBlockParam => MapVeinKind.Fluid,
                _ => null,
            };
        }
    }
}
