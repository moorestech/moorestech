using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class BlockPlacePointCalculator
    {
        public static List<Vector3Int> CalculatePoint(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ)
        {
            // ひとまず、XとZ方向に目的地に向かって1ずつ進む
            var pointList = new List<Vector3Int>();
            var currentPoint = startPoint;
            
            // X軸とZ軸のポイントを設定する
            var startToCornerDistance = 0;
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
            var yDelta = Mathf.Abs(startPoint.y - endPoint.y);
            var currentYDelta = yDelta;
            for (var i = pointList.Count - 1; i >= pointList.Count - yDelta - 1 && i >= 0; i--)
            {
                var point = pointList[i];
                point.y = startPoint.y < endPoint.y ? point.y + currentYDelta : point.y - currentYDelta;
                if (startToCornerDistance + 1 != i)
                {
                    currentYDelta--;
                }
                
                pointList[i] = point;
            }
            
            return pointList;
        }
    }
}