using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Map.MapVein;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction
{
    /// <summary>
    ///     設置対象と鉱脈限定の状態から、鉱脈範囲表示へ「表示種別」と「強調鉱脈」をプッシュする
    ///     Pushes the vein kind and the highlighted vein into the range view, derived from the placement target and the restriction
    /// </summary>
    public class PlacementVeinViewPusher
    {
        private readonly IMapVeinRangeView _mapVeinRangeView;
        private readonly VeinRestrictedPlacementState _veinRestrictedPlacementState;

        public PlacementVeinViewPusher(IMapVeinRangeView mapVeinRangeView, VeinRestrictedPlacementState veinRestrictedPlacementState)
        {
            _mapVeinRangeView = mapVeinRangeView;
            _veinRestrictedPlacementState = veinRestrictedPlacementState;
        }

        public void Push(IPlacementTarget target)
        {
            _mapVeinRangeView.SetVisibleVeinKind(PlacementVeinViewKindResolver.Resolve(target));
            _mapVeinRangeView.SetHighlightedVein(ResolveHighlightedVein(target));

            #region Internal

            // 制限対象ブロックを持っている間だけ対象鉱脈を強調する
            // Highlight the target vein only while the restricted block is the placement target
            Guid? ResolveHighlightedVein(IPlacementTarget placementTarget)
            {
                if (placementTarget is not BlockPlacementTarget blockTarget) return null;
                var blockId = MasterHolder.BlockMaster.GetBlockId(blockTarget.BlockGuid);
                return _veinRestrictedPlacementState.IsRestrictedBlock(blockId) ? _veinRestrictedPlacementState.VeinGuid : null;
            }

            #endregion
        }
    }
}
