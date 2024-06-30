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
            var startToCornerDistance = 0;
            var positions = CalcPositions();
            
            return CalcPlaceDirection(positions);
            
            #region Internal
            
            List<Vector3Int> CalcPositions()
            {
                // ひとまず、XとZ方向に目的地に向かって1ずつ進む
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
                if (startPoint.y == endPoint.y) return pointList;
                
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
                        
                        if (startToCornerDistance + 1 != i) currentYDelta--; // 角の時はY座標を下げない
                        
                        pointList[i] = point;
                    }
                    
                    return pointList;
                }
                
                // 下がる場合
                // if going down
                
                // 下がる場合は、下がり終わる地点が最後から一つ前になるので、最後のポイントはここでは設定しない
                // In case of going down, the last point is not set here because the point where it ends is one before the last.
                // TODo ドキュメント
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
                            VerticalDirection = BlockVerticalDirection.Horizontal,
                        }
                    };
                }
                
                var results = new List<PlaceInfo>(placePositions.Count);
                for (int i = 0; i < placePositions.Count; i++)
                {
                    BlockDirection direction;
                    BlockVerticalDirection verticalDirection;
                    var currentPoint = placePositions[i];
                    
                    
                    // TODo このロジックのドキュメント化
                    if (startPoint.y < endPoint.y)
                        //if (true)
                    {
                        // 上向きの場合
                        if (i == placePositions.Count - 1)
                        {
                            var prevPoint = placePositions[i - 1];
                            (direction, _) = GetBlockDirectionWithNextBlock(prevPoint, currentPoint);
                            verticalDirection = BlockVerticalDirection.Horizontal; // 最後のブロックは必ず水平にする
                        }
                        else
                        {
                            var nextPoint = placePositions[i + 1];
                            (direction, verticalDirection) = GetBlockDirectionWithNextBlock(currentPoint, nextPoint);
                        }
                    }
                    else
                    {
                        // 下向きの場合
                        if ((i == 0 || i == startToCornerDistance) && i != placePositions.Count - 1)
                        {
                            var nextPoint = placePositions[i + 1];
                            (direction, verticalDirection) = GetBlockDirectionWithNextBlock(currentPoint, nextPoint);
                            if (i == startToCornerDistance)
                            {
                                verticalDirection = BlockVerticalDirection.Horizontal; // 角のブロックは必ず水平にする
                            }
                        }
                        else
                        {
                            var prevPoint = placePositions[i - 1];
                            (direction, verticalDirection) = GetBlockDirectionWithNextBlock(prevPoint, currentPoint);
                        }
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