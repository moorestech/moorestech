using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Surface.Grading;
using Game.MapGeneration.Surface;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface.Placement
{
    // 整地footprintが陸に収まる候補だけを採り、重なりはXZで見る
    // Accepts only candidates whose grading footprint stays on land and judges overlap in XZ
    internal sealed class GroundedVeinPlacementRule : IVeinPlacementRule
    {
        private readonly LandCellField _land;
        private readonly Vector3Int _noiseToSceneOffset;
        private readonly SurfaceEnvelope _envelope;
        private readonly WorldSurfaceRevision _revision;
        private int _eligibleCenters;
        private int _evaluated;
        private int _accepted;
        private int _coastRejected;
        private int _edgeRejected;
        private int _overlapRejected;

        internal GroundedVeinPlacementRule(LandCellField land, Vector2 noiseToSceneShift, SurfaceEnvelope envelope, WorldSurfaceRevision revision)
        {
            _land = land;
            _noiseToSceneOffset = PlacementSceneOffset.VeinShift(noiseToSceneShift);
            _envelope = envelope;
            _revision = revision;
        }

        public void BeginCluster()
        {
            _eligibleCenters++;
        }

        public bool TryAcceptMember(PlacedVein candidate, IReadOnlyList<PlacedVein> excludedVeins, IReadOnlyList<PlacedVein> confirmedVeins)
        {
            _evaluated++;

            // 整地と同式でシーン座標へ移し陸地を検査
            // Move to scene space with the grading formula and inspect land
            var footprint = VeinGroundingPlanner.OuterFootprint(candidate.Shifted(_noiseToSceneOffset), _envelope);
            if (!_land.Geometry.Contains(footprint))
            {
                _edgeRejected++;
                return false;
            }
            if (!_land.ContainsSupport(footprint))
            {
                _coastRejected++;
                return false;
            }

            // 隣タイル確定済み、同タイル既出の順にXZ排他
            // XZ exclusion against neighbour-confirmed, then same-tile veins
            if (VeinAabbBuilder.OverlapsAnyXz(candidate, excludedVeins) || VeinAabbBuilder.OverlapsAnyXz(candidate, confirmedVeins))
            {
                _overlapRejected++;
                return false;
            }
            _accepted++;
            return true;
        }

        public void ReportRejections(int seed, int tileX, int tileZ, string entryGuid)
        {
            // 陸地却下か、候補があったのに0件採用のときだけ理由別に記録
            // Record per-reason counts only on land rejections or when candidates existed yet none was accepted
            bool landRejected = 0 < _coastRejected + _edgeRejected;
            bool acceptedNone = (0 < _eligibleCenters || 0 < _evaluated) && _accepted == 0;
            if (landRejected || acceptedNone)
                Debug.LogWarning(
                    $"Vein placement rule seed={seed} revision={_revision} tile={tileX},{tileZ} entry={entryGuid}: eligible centers={_eligibleCenters}, " +
                    $"evaluated={_evaluated}, accepted={_accepted}, coast={_coastRejected}, world-edge={_edgeRejected}, overlap={_overlapRejected}.");
            _eligibleCenters = 0;
            _evaluated = 0;
            _accepted = 0;
            _coastRejected = 0;
            _edgeRejected = 0;
            _overlapRejected = 0;
        }
    }
}
