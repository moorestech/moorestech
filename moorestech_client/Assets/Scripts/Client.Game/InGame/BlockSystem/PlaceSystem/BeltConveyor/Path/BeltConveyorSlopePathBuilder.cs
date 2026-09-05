using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Path
{
    /// <summary>
    /// 坂ブロック選択時の経路計算（全セル坂・一定勾配・地形非依存）
    /// Path calculation while a slope block is selected (all cells sloped, constant grade, terrain-independent)
    /// </summary>
    public static class BeltConveyorSlopePathBuilder
    {
        public static List<PlaceInfo> Build(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ, BlockDirection blockDirection, BlockVerticalDirection slopeDirection)
        {
            // XZは既存ビルダー、Yは一定勾配で決める
            // Build XZ with the existing builder; Y follows a constant grade
            // startPoint.yに揃えた終点を渡すことで、ビルダーの同高早期returnを使いY軸調整をスキップさせる
            // Passing an end point matched to startPoint.y hits the builder's same-height early return, skipping its Y-axis adjustment
            var flatEndPoint = new Vector3Int(endPoint.x, startPoint.y, endPoint.z);
            var (positions, _) = BeltConveyorPositionListBuilder.Build(startPoint, flatEndPoint, isStartDirectionZ);

            var yStep = ResolveYStep(slopeDirection);
            var placeInfos = new List<PlaceInfo>(positions.Count);
            for (var i = 0; i < positions.Count; i++)
            {
                var position = positions[i];
                position.y = startPoint.y + yStep * i;
                placeInfos.Add(new PlaceInfo
                {
                    Position = position,
                    Direction = ResolveDirection(i),
                    VerticalDirection = slopeDirection,
                    Placeable = true,
                });
            }

            return placeInfos;

            #region Internal

            // 勾配は上り下りのみ。Horizontalは坂経路の入力として成立しない
            // Only up and down are grades; Horizontal is not a valid input for a slope run
            int ResolveYStep(BlockVerticalDirection direction)
            {
                switch (direction)
                {
                    case BlockVerticalDirection.Up: return 1;
                    case BlockVerticalDirection.Down: return -1;
                    case BlockVerticalDirection.Horizontal:
                    default:
                        throw new ArgumentOutOfRangeException(nameof(slopeDirection), direction, "BeltConveyorSlopePathBuilder: slope direction must be Up or Down");
                }
            }

            // 進行方向は次セルへの差分、末尾は前セルの差分を継ぐ
            // Facing is the delta to the next cell; the tail inherits the previous delta
            BlockDirection ResolveDirection(int index)
            {
                if (positions.Count == 1) return blockDirection;

                var isTail = index == positions.Count - 1;
                var from = isTail ? positions[index - 1] : positions[index];
                var to = isTail ? positions[index] : positions[index + 1];

                return BeltConveyorDirectionResolver.ResolveHorizontalDirection(from, to);
            }

            #endregion
        }
    }
}
