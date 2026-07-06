using System.Collections.Generic;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor
{
    /// <summary>
    /// 経路座標列から各セルの向き・傾斜（Up/Down/Horizontal）を解決する
    /// Resolves each cell's direction and slope (Up/Down/Horizontal) from the path coordinate list
    /// </summary>
    public static class BeltConveyorDirectionResolver
    {
        public static List<PlaceInfo> Resolve(List<Vector3Int> placePositions, Vector3Int startPoint, Vector3Int endPoint, BlockDirection blockDirection, int startToCornerDistance)
        {
            if (placePositions.Count == 1)
            {
                return new List<PlaceInfo>
                {
                    new PlaceInfo
                    {
                        Position = placePositions[0],
                        Direction = blockDirection,
                        VerticalDirection = BlockVerticalDirection.Horizontal,
                        Placeable = true,
                    },
                };
            }

            var results = new List<PlaceInfo>(placePositions.Count);
            for (var i = 0; i < placePositions.Count; i++)
            {
                BlockDirection direction;
                BlockVerticalDirection verticalDirection;
                var currentPoint = placePositions[i];

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
                    Placeable = true,
                });
            }

            return results;

            #region Internal

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
}
