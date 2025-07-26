using System.Collections.Generic;
using Client.Game.InGame.Block;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class BlockPlacePointCalculator
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        
        public BlockPlacePointCalculator(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }
        
        public List<PlaceInfo> CalculatePoint(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ, BlockDirection blockDirection, BlockMasterElement holdingBlockMasterElement)
        {
            // ひとまず、XとZ方向に目的地に向かって1ずつ進む
            var startToCornerDistance = 0;
            var blockSize = holdingBlockMasterElement.BlockSize;
            var isLargeBlock = blockSize.x > 1 || blockSize.y > 1 || blockSize.z > 1;
            var enableConveyorPlacement = (holdingBlockMasterElement.EnableConveyorPlacement ?? false) && !isLargeBlock;
            
            List<Vector3Int> positions = enableConveyorPlacement ? CalcPositionsForConveyor() : CalcPositions(blockSize);
            
            List<PlaceInfo> result = CalcPlaceDirection(positions);
            result = CalcPlaceable(result);
            
            return result;
            
            #region Internal
            
            List<Vector3Int> CalcPositionsForConveyor()
            {
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
            
            List<Vector3Int> CalcPositions(Vector3Int blockSize)
            {
                var pointList = new List<Vector3Int>();
                var currentPoint = startPoint;
                pointList.Add(currentPoint);
                
                // 最も距離が長い方向を判定
                var deltaX = Mathf.Abs(endPoint.x - startPoint.x);
                var deltaY = Mathf.Abs(endPoint.y - startPoint.y);
                var deltaZ = Mathf.Abs(endPoint.z - startPoint.z);
                
                if (deltaX >= deltaY && deltaX >= deltaZ)
                {
                    // X方向に伸ばす
                    var stepX = blockSize.x;
                    var directionX = endPoint.x > startPoint.x ? 1 : -1;
                    
                    while (Mathf.Abs(currentPoint.x - endPoint.x) >= stepX)
                    {
                        currentPoint.x += stepX * directionX;
                        pointList.Add(currentPoint);
                    }
                }
                else if (deltaZ >= deltaX && deltaZ >= deltaY)
                {
                    // Z方向に伸ばす
                    var stepZ = blockSize.z;
                    var directionZ = endPoint.z > startPoint.z ? 1 : -1;
                    
                    while (Mathf.Abs(currentPoint.z - endPoint.z) >= stepZ)
                    {
                        currentPoint.z += stepZ * directionZ;
                        pointList.Add(currentPoint);
                    }
                }
                else
                {
                    // Y方向に伸ばす
                    var stepY = blockSize.y;
                    var directionY = endPoint.y > startPoint.y ? 1 : -1;
                    
                    while (Mathf.Abs(currentPoint.y - endPoint.y) >= stepY)
                    {
                        currentPoint.y += stepY * directionY;
                        pointList.Add(currentPoint);
                    }
                }
                
                return pointList;
            }
            
            List<PlaceInfo> CalcPlaceDirection(List<Vector3Int> placePositions)
            {
                // enableConveyorPlacementがfalseの場合は初期状態の方向のままにする
                if (!enableConveyorPlacement)
                {
                    var placeInfos = new List<PlaceInfo>(placePositions.Count);
                    
                    foreach (var placePosition in placePositions)
                    {
                        placeInfos.Add(new PlaceInfo
                        {
                            Position = placePosition,
                            Direction = blockDirection,
                            VerticalDirection = BlockVerticalDirection.Horizontal,
                        });
                    }
                    
                    return placeInfos;
                }
                
                if (placePositions.Count == 1)
                {
                    return new List<PlaceInfo>
                    {
                        new()
                        {
                            Position = placePositions[0],
                            Direction = blockDirection,
                            VerticalDirection = BlockVerticalDirection.Horizontal,
                        },
                    };
                }
                
                var results = new List<PlaceInfo>(placePositions.Count);
                for (var i = 0; i < placePositions.Count; i++)
                {
                    BlockDirection direction;
                    BlockVerticalDirection verticalDirection;
                    var currentPoint = placePositions[i];
                    
                    
                    // TODo このロジックのドキュメント化
                    if (startPoint.y < endPoint.y)
                    {
                        // 上向きの場合
                        if (i == 0 && placePositions.Count == 2)
                        {
                            var nextPoint = placePositions[i + 1];
                            (direction, _) = GetBlockDirectionWithNextBlock(currentPoint, nextPoint);
                            verticalDirection = BlockVerticalDirection.Up;
                        }
                        else if (i == 0 && placePositions.Count > 1) // 上がる場合、最初のブロックは必ず水平になる
                        {
                            var nextPoint = placePositions[i + 1];
                            (direction, _) = GetBlockDirectionWithNextBlock(currentPoint, nextPoint);
                            verticalDirection = BlockVerticalDirection.Horizontal; // 最後のブロックは必ず水平にする
                        }
                        else if (i == placePositions.Count - 1)
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
                    else if (startPoint.y > endPoint.y) // 下向きの場合
                    {
                        if (i == 0 && placePositions.Count == 2) // 最初のブロックかつ、2個のブロックかつ、全体が下向きの場合は、ブロックを下向きにする
                        {
                            var nextPoint = placePositions[i + 1];
                            (direction, _) = GetBlockDirectionWithNextBlock(currentPoint, nextPoint);
                            verticalDirection = BlockVerticalDirection.Down;
                        }
                        else if (i == 0) // 下がる場合、最初のブロックは必ず水平になる
                        {
                            var nextPoint = placePositions[i + 1];
                            (direction, _) = GetBlockDirectionWithNextBlock(currentPoint, nextPoint);
                            verticalDirection = BlockVerticalDirection.Horizontal;
                        }
                        else if (i == startToCornerDistance && i != placePositions.Count - 1)
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
                    else // 水平の場合
                    {
                        if (i != placePositions.Count - 1)
                        {
                            var nextPoint = placePositions[i + 1];
                            (direction, verticalDirection) = GetBlockDirectionWithNextBlock(currentPoint, nextPoint);
                        }
                        else
                        {
                            var prevPoint = placePositions[i - 1];
                            (direction, verticalDirection) = GetBlockDirectionWithNextBlock(prevPoint, currentPoint);
                        }
                    }
                    
                    results.Add(new PlaceInfo
                    {
                        Position = currentPoint,
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
            
            List<PlaceInfo> CalcPlaceable(List<PlaceInfo> infos)
            {
                foreach (var info in infos)
                {
                    //TODO ブロックの数が足りているかどうか
                    info.Placeable = IsNotExistBlock(info);
                }
                
                return infos;
            }
            
            // 設置予定地にブロックが既に存在しているかどうか
            bool IsNotExistBlock(PlaceInfo placeInfo)
            {
                // 設置の縦方向のguidを取得
                var blockId = holdingBlockMasterElement.BlockGuid.GetVerticalOverrideBlockId(placeInfo.VerticalDirection);
                
                var size = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize;
                var previewPositionInfo = new BlockPositionInfo(placeInfo.Position, placeInfo.Direction, size);
                
                return !_blockGameObjectDataStore.IsOverlapPositionInfo(previewPositionInfo);
            }
            
            #endregion
        }
    }
}