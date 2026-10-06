using Game.MapGeneration.Surface;
using UnityEngine;

namespace Client.Game.InGame.Map.Outcrop
{
    // 露頭prefabが表示契約の接地条件を満たすかをロード時に1度だけ判定する
    // Judges once at load time whether an outcrop prefab meets the presentation contract's grounding terms
    internal sealed class OutcropGroundingContract : ITerrainSurfacePresentationVisitor
    {
        private readonly string _prefabName;
        private readonly bool _hasMeshBounds;
        private readonly Bounds _meshBounds;
        public string Violation { get; private set; }

        internal OutcropGroundingContract(string prefabName, bool hasMeshBounds, Bounds meshBounds)
        {
            _prefabName = prefabName;
            _hasMeshBounds = hasMeshBounds;
            _meshBounds = meshBounds;
        }

        public void VisitLegacy()
        {
            // 旧版は接地しないので寸法を問わない
            // Legacy never grounds outcrops, so their size is not constrained
        }

        public void VisitGrounded(SurfaceEnvelope envelope)
        {
            // 平坦coreに収まる有効meshだけが接地を保証できる
            // Only an enabled mesh fitting the flat core can guarantee grounding
            if (!_hasMeshBounds)
                Violation = $"No enabled MeshRenderer on outcrop prefab {_prefabName}.";
            else if (envelope.CoreHalfSize * 2f < _meshBounds.size.x || envelope.CoreHalfSize * 2f < _meshBounds.size.z)
                Violation = $"Outcrop prefab {_prefabName} exceeds the grading core: {_meshBounds.size}.";
        }
    }
}
