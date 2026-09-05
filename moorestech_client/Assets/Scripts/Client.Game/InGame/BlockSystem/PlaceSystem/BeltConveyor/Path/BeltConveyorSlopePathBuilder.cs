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
            // XZ経路だけを既存ビルダーで組み、Yは終点の高さを見ずに一定勾配で決める
            // Build only the XZ path with the existing builder; Y follows a constant grade ignoring the end height
            var flatEndPoint = new Vector3Int(endPoint.x, startPoint.y, endPoint.z);
            var (positions, _) = BeltConveyorPositionListBuilder.Build(startPoint, flatEndPoint, isStartDirectionZ);

            var yStep = slopeDirection == BlockVerticalDirection.Up ? 1 : -1;
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

            // 進行方向は次セルへの差分。末尾セルだけ前セルからの差分を引き継ぐ
            // The facing comes from the delta to the next cell; the tail inherits the delta from the previous cell
            BlockDirection ResolveDirection(int index)
            {
                if (positions.Count == 1) return blockDirection;

                var isTail = index == positions.Count - 1;
                var from = isTail ? positions[index - 1] : positions[index];
                var to = isTail ? positions[index] : positions[index + 1];

                if (from.x == to.x) return to.z > from.z ? BlockDirection.North : BlockDirection.South;
                return to.x > from.x ? BlockDirection.East : BlockDirection.West;
            }

            #endregion
        }
    }
}
