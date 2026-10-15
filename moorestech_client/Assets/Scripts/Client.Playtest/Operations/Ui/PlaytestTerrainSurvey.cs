using Client.Common;
using UnityEngine;

namespace Client.Playtest.Operations.Ui
{
    /// <summary>
    ///     生成地形から、設置検証に使う「+X方向に上る斜面の区間」を探す
    ///     Finds a run of generated terrain rising along +X for placement checks
    /// </summary>
    public static class PlaytestTerrainSurvey
    {
        private const float ProbeStartHeight = 1000f;
        private const float ProbeDistance = 1500f;

        // 中心から近い順に、length セルの区間で始点→終点の上昇が minRise..maxRise かつ遮蔽物の無い区間を探す
        // Nearest-first, find a length-cell run whose start-to-end rise is within minRise..maxRise with nothing standing on it
        public static bool TryFindRisingRunAlongX(Vector2Int center, int searchRadius, int length, float minRise, float maxRise, int excludedHalfExtent, out TerrainSlopeRun run)
        {
            run = default;
            for (var ring = 0; ring <= searchRadius; ring += 2)
            for (var dz = -ring; dz <= ring; dz += 2)
            for (var dx = -ring; dx <= ring; dx += 2)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring) continue;
                var start = new Vector2Int(center.x + dx, center.y + dz);
                if (Mathf.Abs(start.y) < excludedHalfExtent && start.x < excludedHalfExtent && -excludedHalfExtent < start.x + length) continue;
                if (TryMeasureRun(start, length, out var heights) && IsRising(heights))
                {
                    run = new TerrainSlopeRun(start, heights);
                    return true;
                }
            }
            return false;

            #region Internal

            bool IsRising(float[] heights)
            {
                // 単調に上り、始点→終点の上昇が範囲内の区間だけを採る
                // Accept only monotonically rising runs whose total rise is within range
                for (var i = 1; i < heights.Length; i++) if (heights[i] < heights[i - 1] - 0.05f) return false;
                var rise = heights[heights.Length - 1] - heights[0];
                return minRise <= rise && rise <= maxRise;
            }

            #endregion
        }

        // 各セル中心の地表高さを測る。最初に当たるのが地形でなければ（木・岩・ブロック等）その区間は使わない
        // Measure each cell-center surface height; reject the run if the first hit is not terrain (tree, rock, block, ...)
        public static bool TryMeasureRun(Vector2Int start, int length, out float[] heights)
        {
            heights = new float[length];
            for (var i = 0; i < length; i++)
            {
                var ray = new Ray(new Vector3(start.x + i + 0.5f, ProbeStartHeight, start.y + 0.5f), Vector3.down);
                if (!Physics.Raycast(ray, out var hit, ProbeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
                if (hit.collider.gameObject.layer != LayerConst.GroundLayer) return false;
                heights[i] = hit.point.y;
            }
            return true;
        }
    }

    public readonly struct TerrainSlopeRun
    {
        public readonly Vector2Int StartCell;
        public readonly float[] Heights;

        public TerrainSlopeRun(Vector2Int startCell, float[] heights)
        {
            StartCell = startCell;
            Heights = heights;
        }

        public Vector3 SurfacePoint(int index) => new(StartCell.x + index + 0.5f, Heights[index], StartCell.y + 0.5f);
        public string Describe() => $"start=({StartCell.x},{StartCell.y}) heights=[{string.Join(",", System.Array.ConvertAll(Heights, h => h.ToString("F2")))}]";
    }
}
