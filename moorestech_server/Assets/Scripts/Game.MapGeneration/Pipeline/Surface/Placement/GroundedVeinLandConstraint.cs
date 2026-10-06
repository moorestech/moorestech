using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Surface.Grading;
using Game.MapGeneration.Surface;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface.Placement
{
    public sealed class GroundedVeinLandConstraint : IVeinLandConstraint
    {
        private readonly LandCellField _land;
        private readonly Vector3Int _noiseToSceneOffset;
        private readonly SurfaceEnvelope _envelope;
        private int _eligibleCenters;
        private int _candidates;
        private int _coastRejected;
        private int _edgeRejected;

        public GroundedVeinLandConstraint(LandCellField land, Vector2 noiseToSceneShift, SurfaceEnvelope envelope)
        {
            _land = land;
            _noiseToSceneOffset = PlacementSceneOffset.VeinShift(noiseToSceneShift);
            _envelope = envelope;
        }

        public void RecordEligibleCenter()
        {
            _eligibleCenters++;
        }

        public bool Overlaps(PlacedVein candidate, IReadOnlyList<PlacedVein> veins)
        {
            return VeinAabbBuilder.OverlapsAnyXz(candidate, veins);
        }

        public bool Accept(PlacedVein noiseSpaceVein)
        {
            _candidates++;
            // 整地と同式でシーン座標へ移し検査
            // Move to scene space with the grading formula and inspect
            var footprint = VeinGroundingPlanner.OuterFootprint(noiseSpaceVein.Shifted(_noiseToSceneOffset), _envelope);
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
            return true;
        }

        public void ReportRejections(int seed, int tileX, int tileZ, string entryGuid, int acceptedCount)
        {
            // 有限候補の却下理由をタイル単位で記録
            // Record rejection reasons of finite candidates per tile
            if (0 < _coastRejected + _edgeRejected)
                Debug.LogWarning($"Vein land constraint seed={seed} revision=Grounded5 tile={tileX},{tileZ} entry={entryGuid}: coast={_coastRejected}, world-edge={_edgeRejected}.");
            else if ((0 < _eligibleCenters || 0 < _candidates) && acceptedCount == 0)
                Debug.LogWarning($"Vein candidates seed={seed} revision=Grounded5 tile={tileX},{tileZ} entry={entryGuid}: {_candidates} member candidates accepted zero; eligible centers={_eligibleCenters}.");
            _eligibleCenters = 0;
            _candidates = 0;
            _coastRejected = 0;
            _edgeRejected = 0;
        }
    }
}
