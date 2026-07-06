using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor
{
    /// <summary>
    /// 開始点から終了点までの1マス刻みの経路座標列を組み立てる（カーブ・傾斜のY軸調整込み）
    /// Builds the grid-step path from start to end (including the Y-axis adjustment for curves/slopes)
    /// </summary>
    public static class BeltConveyorPositionListBuilder
    {
        public static (List<Vector3Int> positions, int startToCornerDistance) Build(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ)
        {
            var startToCornerDistance = 0;
            var pointList = new List<Vector3Int>();
            var currentPoint = startPoint;

            // X軸とZ軸のポイントを設定する
            pointList.Add(currentPoint);
            while (currentPoint.x != endPoint.x || currentPoint.z != endPoint.z)
            {
                // 指定された方向（X or Z）に伸ばす
                if (isStartDirectionZ && currentPoint.z != endPoint.z)
                {
                    currentPoint.z += endPoint.z > currentPoint.z ? 1 : -1;
                    startToCornerDistance++;
                }
                else if (!isStartDirectionZ && currentPoint.x != endPoint.x)
                {
                    currentPoint.x += endPoint.x > currentPoint.x ? 1 : -1;
                    startToCornerDistance++;
                }
                else
                {
                    // 直角に曲がり、もう片方の軸に向かう
                    if (currentPoint.z != endPoint.z)
                    {
                        currentPoint.z += endPoint.z > currentPoint.z ? 1 : -1;
                    }
                    else
                    {
                        currentPoint.x += endPoint.x > currentPoint.x ? 1 : -1;
                    }
                }

                pointList.Add(currentPoint);
            }

            // Y軸を設定する
            // set Y axis

            // 同じ高さの場合はそのまま返す
            // return as it is if the same height
            if (startPoint.y == endPoint.y) return (pointList, startToCornerDistance);

            var yDelta = Mathf.Abs(startPoint.y - endPoint.y);
            var currentYDelta = yDelta;

            // 上がる場合
            // if going up
            if (startPoint.y < endPoint.y)
            {
                // 逆ループしながら下がるようにY座標を設定する
                for (var i = pointList.Count - 1; i >= 0 && currentYDelta > 0; i--)
                {
                    var point = pointList[i];
                    point.y += currentYDelta;

                    if (startToCornerDistance + 1 != i || pointList.Count == 2) currentYDelta--; // 角の時はY座標を下げない

                    pointList[i] = point;
                }

                // 上がる場合、最初のポイントは次のポイントと同じ高さにする、ただし、2つ目の場合は一つ下にする
                if (pointList.Count > 1 && pointList.Count != 2)
                {
                    var firstPoint = pointList[0];
                    var secondPoint = pointList[1];
                    firstPoint.y = secondPoint.y;
                    pointList[0] = firstPoint;
                }

                return (pointList, startToCornerDistance);
            }

            // 下がる場合
            // if going down

            // 下がる場合は、下がり終わる地点が最後から一つ前になるので、最後のポイントはここでは設定しない
            // In case of going down, the last point is not set here because the point where it ends is one before the last.
            var minusIndex = 2;
            for (var i = pointList.Count - minusIndex; i >= 0 && currentYDelta > 0; i--)
            {
                var point = pointList[i];

                if (startToCornerDistance != i)
                {
                    point.y -= currentYDelta;
                    currentYDelta--; // 角の時はY座標を下げない
                }
                else
                {
                    point.y -= currentYDelta;
                }

                pointList[i] = point;
            }

            // 最後のポイントを設定
            // set the last point
            var lastPoint = pointList[^1];
            lastPoint.y = endPoint.y;
            pointList[^1] = lastPoint;

            return (pointList, startToCornerDistance);
        }
    }
}
