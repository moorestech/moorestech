using System.Collections.Generic;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class BlockPlacePointCalculator
    {
        public static List<PlaceInfo> CalculatePoint(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ, BlockDirection blockDirection)
        {
            // ひとまず、XとZ方向に目的地に向かって1ずつ進む
            var positions = CalcPositions();
            
            return CalcPlaceDirection(positions);
            
            #region Internal
            
            List<Vector3Int> CalcPositions()
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
            
            List<PlaceInfo> CalcPlaceDirection(List<Vector3Int> placePositions)
            {
                if (placePositions.Count == 1)
                {
                    return new List<PlaceInfo>()
                    {
                        new()
                        {
                            Point = placePositions[0],
                            Direction = blockDirection,
                        }
                    };
                }
                
                var results = new List<PlaceInfo>();
                for (int i = 0; i < placePositions.Count; i++)
                {
                    BlockDirection direction;
                    BlockVerticalDirection verticalDirection;
                    var currentPoint = placePositions[i];
                    if (i == placePositions.Count - 1)
                    {
                        var prevPoint = placePositions[i - 1];
                        (direction, verticalDirection) = GetBlockDirectionWithNextBlock(prevPoint, currentPoint);
                    }
                    else
                    {
                        var nextPoint = placePositions[i + 1];
                        (direction, verticalDirection) = GetBlockDirectionWithNextBlock(currentPoint, nextPoint);
                    }
                    
                    results.Add(new PlaceInfo()
                    {
                        Point = currentPoint,
                        Direction = direction,
                        VerticalDirection = verticalDirection,
                    });
                }
                
                return results;
            }
            
            (BlockDirection direction, BlockVerticalDirection verticalDirection) GetBlockDirectionWithNextBlock(Vector3Int currentPoint, Vector3Int nextPoint)
            {
                var horizonDirection = BlockDirection.North;
                if (currentPoint.x == nextPoint.x)
                {
                    horizonDirection = nextPoint.z > currentPoint.z ? BlockDirection.North : BlockDirection.South;
                }
                else
                {
                    horizonDirection = nextPoint.x > currentPoint.x ? BlockDirection.East : BlockDirection.West;
                }
                
                BlockVerticalDirection verticalDirection;
                if (currentPoint.y == nextPoint.y)
                {
                    verticalDirection = BlockVerticalDirection.Horizontal;
                }
                else
                {
                    verticalDirection = currentPoint.y < nextPoint.y ? BlockVerticalDirection.Up : BlockVerticalDirection.Down;
                }
                
                return (horizonDirection, verticalDirection);
            }
            
            #endregion
        }
    }
    
    public class PlaceInfo
    {
        public Vector3Int Point { get; set; }
        public BlockDirection Direction { get; set; }
        
        public BlockVerticalDirection VerticalDirection { get; set; }
    }
    
    public enum BlockVerticalDirection
    {
        Up,
        Horizontal,
        Down,
    }
}