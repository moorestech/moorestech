using Client.Game.InGame.Environment.Terrain;
using Game.MapGeneration.Surface;
using UnityEngine;

namespace Client.Game.InGame.Map.Outcrop
{
    // 露頭1体を表示契約に合わせて地表へ置く。寸法と描画範囲はロード段で検査済みの入力だけを使う
    // Places one outcrop on the surface per the presentation contract, using only extents already validated at load time
    public sealed class OutcropSurfacePlacement : ITerrainSurfacePresentationVisitor
    {
        // 露頭の底を地表のZファイトから浮かせる量（採掘底面の余裕とは別の意味）
        // How far an outcrop base is lifted off the ground to avoid z-fighting (unrelated to the mining clearance)
        private const float OutcropGroundLiftMeters = 0.001f;

        private readonly GameObject _instance;
        private readonly OutcropPrefab _outcrop;
        private readonly Bounds _veinBounds;

        private OutcropSurfacePlacement(GameObject instance, OutcropPrefab outcrop, Bounds veinBounds)
        {
            _instance = instance;
            _outcrop = outcrop;
            _veinBounds = veinBounds;
        }

        public static void Place(GameObject instance, OutcropPrefab outcrop, Bounds veinBounds, TerrainSurfacePresentation presentation)
        {
            presentation.Accept(new OutcropSurfacePlacement(instance, outcrop, veinBounds));
        }

        public void VisitLegacy()
        {
            // 旧worldは作者pivotの位置を変えない
            // Legacy worlds keep the authored pivot position
        }

        public void VisitGrounded(SurfaceEnvelope envelope)
        {
            // 接地契約の検査はロード段(OutcropGameObjectDatastore)の一箇所だけで行い、違反prefabはここへ届かない
            // The grounding contract is checked only at load time (OutcropGameObjectDatastore); violating prefabs never reach here
            var center = _veinBounds.center;
            var terrain = FindContainingTerrain();
            var groundHeight = terrain.SampleHeight(center) + terrain.transform.position.y;

            // 全meshの中心と底を平坦coreへ
            // Align the whole mesh center and bottom with the flat core
            var meshBounds = _outcrop.MeshBounds;
            var position = _instance.transform.position;
            var shift = new Vector3(center.x - (position.x + meshBounds.center.x),
                groundHeight + OutcropGroundLiftMeters - (position.y + meshBounds.min.y), center.z - (position.z + meshBounds.center.z));
            _instance.transform.position += shift;

            #region Internal

            Terrain FindContainingTerrain()
            {
                Terrain selected = null;
                foreach (var candidate in Terrain.activeTerrains)
                {
                    var origin = candidate.transform.position;
                    var size = candidate.terrainData.size;
                    if (center.x < origin.x || origin.x + size.x < center.x ||
                        center.z < origin.z || origin.z + size.z < center.z) continue;

                    // 共有境界は原点の辞書順で選ぶ
                    // Select shared boundaries by lexicographic terrain origin
                    if (selected == null || origin.x < selected.transform.position.x ||
                        (origin.x == selected.transform.position.x && origin.z < selected.transform.position.z))
                        selected = candidate;
                }
                if (selected == null)
                    throw SurfaceContractFailure.Create($"[OutcropSurfacePlacement] No terrain contains {center}.");
                return selected;
            }

            #endregion
        }
    }
}
