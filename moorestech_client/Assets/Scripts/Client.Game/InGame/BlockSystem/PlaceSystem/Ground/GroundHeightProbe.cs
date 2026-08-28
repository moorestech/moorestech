using Client.Common;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Ground
{
    /// <summary>
    ///     地表探査の単一の持ち主。設置系の具体実装から独立して地形の高さだけを答える
    ///     The single owner of ground probing; it answers terrain heights independently of any concrete placement system
    /// </summary>
    public static class GroundHeightProbe
    {
        public static readonly int GroundLayerMask = LayerMask.GetMask("Ground");

        // 地表探査のレイ始点高さと探査距離。地形の最高点より十分上から、最低点より下まで貫く
        // Ray start height and probe length of ground probing: from well above the highest terrain to below the lowest
        private const float GroundProbeStartHeight = 1000f;
        private const float GroundProbeDistance = 1500f;

        // セル境界ちょうどを探査すると隣接セルの地形を拾うため、探査点をセル内側へ寄せる
        // Probing exactly on a cell border would pick up the neighbouring cell's terrain, so probe points are inset
        private const float CellCornerInset = 0.01f;

        // 探査レイの描画色。呼び出し側から渡すと消費者不在の引数が残るため所有者側に固定する
        // Probe ray color, fixed on the owner because passing it in leaves a parameter with no consumer
        private static readonly Color GroundProbeRayColor = Color.red;

        // XZだけを取りY成分の取り違えを署名で封じる。露頭など大量プローブ用にログ無しで成否を返す
        // Taking only XZ makes a mistaken Y impossible, and bulk probes such as outcrops get the outcome without logging
        public static bool TryGetGroundPoint(float worldX, float worldZ, out Vector3 groundPoint)
        {
            var checkRay = new Ray(new Vector3(worldX, GroundProbeStartHeight, worldZ), Vector3.down);
            if (Physics.Raycast(checkRay, out var checkHit, GroundProbeDistance, GroundLayerMask))
            {
                groundPoint = checkHit.point;
                return true;
            }
            groundPoint = default;
            return false;
        }

        // 探査失敗をログで知らせる入口。XZ明示なのはVector3を取るとVector2の暗黙変換でz=0を探査できてしまうため
        // Entry point that logs a failed probe; it takes XZ because a Vector3 parameter would let the Vector2 conversion probe z=0
        public static Vector3? GetGroundPoint(float worldX, float worldZ)
        {
            Debug.DrawRay(new Vector3(worldX, GroundProbeStartHeight, worldZ), Vector3.down * GroundProbeDistance, GroundProbeRayColor, 3);

            if (!TryGetGroundPoint(worldX, worldZ, out var groundPoint))
            {
                Debug.LogError($"地面が見つかりませんでした x:{worldX} z:{worldZ} layer:{GroundLayerMask}");
                return null;
            }
            return groundPoint;
        }

        // ブロックが占有するセルの地形だけから最高点を出す。占有していない隣接セルの地形でYを持ち上げない
        // Takes the max height from the terrain of the occupied cells only, so a neighbouring cell never lifts Y
        public static bool TryGetFootprintMaxGroundHeight(Vector3Int blockPos, BlockDirection blockDirection, Vector3Int blockSize, out float maxHeight)
        {
            maxHeight = float.NegativeInfinity;
            var (minPos, maxPos) = blockPos.GetWorldBlockBoundingBox(blockDirection, blockSize);

            // boundingBoxは3次元なので水平の占有セルはXとZで組む。Vector2の暗黙変換に任せると鉛直Yを渡してz=0を探査してしまう
            // The bounding box is 3D, so the occupied cells pair X with Z; the Vector2 conversion would pass the vertical Y and probe z=0
            var minCellX = Mathf.FloorToInt(minPos.x);
            var minCellZ = Mathf.FloorToInt(minPos.z);
            var maxCellX = Mathf.CeilToInt(maxPos.x);
            var maxCellZ = Mathf.CeilToInt(maxPos.z);

            for (var cellX = minCellX; cellX < maxCellX; cellX++)
            {
                for (var cellZ = minCellZ; cellZ < maxCellZ; cellZ++)
                {
                    if (!TryProbeCellMaxHeight(cellX, cellZ, out var cellHeight)) return false;
                    maxHeight = Mathf.Max(maxHeight, cellHeight);
                }
            }

            return !float.IsNegativeInfinity(maxHeight);

            #region Internal

            // 1セルの地形は内側四隅で測る。平面の地形ならこの4点に最高点が現れる
            // One cell's terrain is measured at its inset corners, where a planar terrain's maximum shows up
            bool TryProbeCellMaxHeight(int cellX, int cellZ, out float height)
            {
                height = float.NegativeInfinity;
                var lowX = cellX + CellCornerInset;
                var highX = cellX + 1f - CellCornerInset;
                var lowZ = cellZ + CellCornerInset;
                var highZ = cellZ + 1f - CellCornerInset;

                if (!TryProbeHeight(lowX, lowZ, ref height)) return false;
                if (!TryProbeHeight(lowX, highZ, ref height)) return false;
                if (!TryProbeHeight(highX, lowZ, ref height)) return false;
                if (!TryProbeHeight(highX, highZ, ref height)) return false;

                return true;
            }

            bool TryProbeHeight(float worldX, float worldZ, ref float height)
            {
                if (!TryGetGroundPoint(worldX, worldZ, out var groundPoint)) return false;

                height = Mathf.Max(height, groundPoint.y);
                return true;
            }

            #endregion
        }
    }
}
