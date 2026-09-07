using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
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
        public static List<PlaceInfo> Build(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ, BlockDirection blockDirection, BeltSlopeGrade slopeGrade)
        {
            // XZは水平専用の組み立てを使い、Yは一定勾配で決める
            // Build XZ with the horizontal-only layout; Y follows a constant grade
            var (positions, _) = BeltConveyorPositionListBuilder.BuildHorizontalPositions(startPoint, endPoint, isStartDirectionZ);

            var yStep = slopeGrade == BeltSlopeGrade.Up ? 1 : -1;
            var verticalDirection = slopeGrade == BeltSlopeGrade.Up ? BlockVerticalDirection.Up : BlockVerticalDirection.Down;
            var placeInfos = new List<PlaceInfo>(positions.Count);
            for (var i = 0; i < positions.Count; i++)
            {
                var position = positions[i];
                position.y = startPoint.y + yStep * i;
                placeInfos.Add(new PlaceInfo
                {
                    Position = position,
                    Direction = ResolveDirection(i),
                    VerticalDirection = verticalDirection,
                    Placeable = true,
                });
            }

            return placeInfos;

            #region Internal

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
