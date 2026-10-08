using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run
{
    public readonly struct PlacementRunPositions
    {
        public readonly IReadOnlyList<Vector3Int> Positions;
        public readonly PlacementRunAxis Axis;
        public readonly int CursorIndex;

        public PlacementRunPositions(IReadOnlyList<Vector3Int> positions, PlacementRunAxis axis, int cursorIndex)
        {
            Positions = positions;
            Axis = axis;
            CursorIndex = cursorIndex;
        }
    }

    /// <summary>
    ///     ドラッグの長軸に沿って列位置を作る
    ///     Builds run positions along the drag's longest axis
    /// </summary>
    public static class PlacementRunPositionCalculator
    {
        public static PlacementRunPositions Calculate(Vector3Int startPoint, Vector3Int endPoint, Vector3Int stepSize)
        {
            var positions = new List<Vector3Int> { startPoint };
            var current = startPoint;
            var deltaX = Mathf.Abs(endPoint.x - startPoint.x);
            var deltaY = Mathf.Abs(endPoint.y - startPoint.y);
            var deltaZ = Mathf.Abs(endPoint.z - startPoint.z);

            // 最も長い軸だけへ外形寸法ずつ伸ばす
            // Extend only the longest axis by its footprint component
            PlacementRunAxis axis;
            if (deltaX >= deltaY && deltaX >= deltaZ)
            {
                axis = PlacementRunAxis.X;
                var direction = endPoint.x > startPoint.x ? 1 : -1;
                while (Mathf.Abs(current.x - endPoint.x) >= stepSize.x)
                {
                    current.x += stepSize.x * direction;
                    positions.Add(current);
                }
            }
            else if (deltaZ >= deltaX && deltaZ >= deltaY)
            {
                axis = PlacementRunAxis.Z;
                var direction = endPoint.z > startPoint.z ? 1 : -1;
                while (Mathf.Abs(current.z - endPoint.z) >= stepSize.z)
                {
                    current.z += stepSize.z * direction;
                    positions.Add(current);
                }
            }
            else
            {
                axis = PlacementRunAxis.Y;
                var direction = endPoint.y > startPoint.y ? 1 : -1;
                while (Mathf.Abs(current.y - endPoint.y) >= stepSize.y)
                {
                    current.y += stepSize.y * direction;
                    positions.Add(current);
                }
            }

            return new PlacementRunPositions(positions, axis, ResolveCursorIndex(positions, endPoint));

            #region Internal

            // 終点が刻みに乗らない場合は末尾を指す
            // Point at the last cell if the cursor misses the stride
            static int ResolveCursorIndex(List<Vector3Int> runPositions, Vector3Int cursor)
            {
                for (var i = 0; i < runPositions.Count; i++)
                {
                    if (runPositions[i] == cursor) return i;
                }

                return runPositions.Count - 1;
            }

            #endregion
        }
    }
}
