using Game.MapGeneration.Facade.Surface;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface.Placement
{
    public sealed class GroundedVeinLandConstraint : IVeinLandConstraint
    {
        private readonly LandCellField _land;
        private readonly Vector2 _noiseToSceneShift;
        private readonly float _outerHalfSize;
        private int _coastRejected;
        private int _edgeRejected;

        public GroundedVeinLandConstraint(LandCellField land, Vector2 noiseToSceneShift, SurfaceEnvelope envelope)
        {
            _land = land;
            _noiseToSceneShift = new Vector2(Mathf.RoundToInt(noiseToSceneShift.x), Mathf.RoundToInt(noiseToSceneShift.y));
            _outerHalfSize = envelope.CoreHalfSize + envelope.BlendWidth;
        }

        public bool Accept(PlacedVein noiseSpaceVein)
        {
            // inclusive中心を一度だけシーン座標へ移し、接続部まで検査する
            // Convert the inclusive center once into scene space and inspect the entire skirt
            var center = (Vector3)(noiseSpaceVein.Min + noiseSpaceVein.Max + Vector3Int.one) * 0.5f;
            var scene = new Vector2(center.x, center.z) - _noiseToSceneShift;
            var footprint = new Rect(scene - Vector2.one * _outerHalfSize, Vector2.one * (2f * _outerHalfSize));
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

        public void ReportRejections(int seed, int tileX, int tileZ, string entryGuid)
        {
            // 有限候補の却下理由をエントリとタイル単位で残す
            // Record finite candidate rejections per entry and tile
            if (_coastRejected + _edgeRejected > 0)
                Debug.LogWarning($"Vein land constraint seed={seed} revision=Grounded5 tile={tileX},{tileZ} entry={entryGuid}: coast={_coastRejected}, world-edge={_edgeRejected}.");
            _coastRejected = 0;
            _edgeRejected = 0;
        }
    }
}
