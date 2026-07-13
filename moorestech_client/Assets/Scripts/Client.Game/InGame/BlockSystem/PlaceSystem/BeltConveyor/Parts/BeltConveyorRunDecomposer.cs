using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// 1マス刻みの経路セル列を長尺バリアント・斜面ブロックの設置エンティティ列へ分解する純ロジック
    /// Pure logic that decomposes per-cell path infos into length-variant and slope placement entities
    /// </summary>
    public static class BeltConveyorRunDecomposer
    {
        public static List<PlaceInfo> Decompose(IReadOnlyList<PlaceInfo> cells, BeltConveyorFamily family)
        {
            var straightVariantsDesc = family.StraightVariantsDesc;
            var result = new List<PlaceInfo>();
            var index = 0;
            while (index < cells.Count)
            {
                var cell = cells[index];

                // 斜面・設置不可セルは1マスエンティティとして確定
                // Slope cells and unplaceable cells become single-cell entities
                if (cell.VerticalDirection == BlockVerticalDirection.Up) { result.Add(CreateSlope(cell, family.UpBlockId)); index++; continue; }
                if (cell.VerticalDirection == BlockVerticalDirection.Down) { result.Add(CreateSlope(cell, family.DownBlockId)); index++; continue; }
                if (!cell.Placeable) { result.Add(CreateSingle(cell, GetLengthOneBlockId())); index++; continue; }

                // 水平ランの長さを検出し、長い順の貪欲割当で最小ブロック数に分解
                // Detect the horizontal run length, then greedily assign longest variants first
                var runLength = DetectRunLength(index);
                var offset = 0;
                while (offset < runLength)
                {
                    var (variantLength, variantBlockId) = SelectVariant(runLength - offset);
                    result.Add(CreateStraight(index + offset, variantLength, variantBlockId));
                    offset += variantLength;
                }
                index += runLength;
            }

            return result;

            #region Internal

            int DetectRunLength(int startIndex)
            {
                var length = 1;
                while (startIndex + length < cells.Count && IsRunContinuation(cells[startIndex + length - 1], cells[startIndex + length])) length++;
                return length;
            }

            bool IsRunContinuation(PlaceInfo current, PlaceInfo next)
            {
                if (next.VerticalDirection != BlockVerticalDirection.Horizontal || !next.Placeable) return false;
                if (next.Direction != current.Direction) return false;
                // 進行方向に1マスちょうど隣接していることを要求（高さ変化や飛びは分割）
                // Require exact one-cell adjacency along the travel direction (splits on height jumps/gaps)
                return next.Position - current.Position == ToVector(current.Direction);
            }

            (int length, BlockId blockId) SelectVariant(int remaining)
            {
                foreach (var variant in straightVariantsDesc)
                {
                    if (variant.length <= remaining) return variant;
                }
                return (1, GetLengthOneBlockId());
            }

            BlockId GetLengthOneBlockId()
            {
                return straightVariantsDesc[straightVariantsDesc.Count - 1].blockId;
            }

            PlaceInfo CreateStraight(int startIndex, int length, BlockId blockId)
            {
                // マルチセルブロックの原点は占有範囲の最小座標（BlockPositionInfoの規約）
                // Multi-cell block origin is the min corner of the occupied range (BlockPositionInfo convention)
                var origin = cells[startIndex].Position;
                for (var i = 1; i < length; i++) origin = Vector3Int.Min(origin, cells[startIndex + i].Position);

                return new PlaceInfo
                {
                    Position = origin,
                    Direction = cells[startIndex].Direction,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    Placeable = true,
                    BlockId = blockId,
                };
            }

            PlaceInfo CreateSlope(PlaceInfo cell, BlockId? slopeBlockId)
            {
                // 斜面バリアントを持たないファミリー（分岐器など）では傾斜セルを設置不可にする
                // Families without slope variants (e.g. splitters) cannot place sloped cells
                if (slopeBlockId == null)
                {
                    var unplaceableCell = CreateSingle(cell, GetLengthOneBlockId());
                    unplaceableCell.Placeable = false;
                    return unplaceableCell;
                }

                return CreateSingle(cell, slopeBlockId.Value);
            }

            PlaceInfo CreateSingle(PlaceInfo cell, BlockId blockId)
            {
                return new PlaceInfo
                {
                    Position = cell.Position,
                    Direction = cell.Direction,
                    VerticalDirection = cell.VerticalDirection,
                    Placeable = cell.Placeable,
                    BlockId = blockId,
                };
            }

            Vector3Int ToVector(BlockDirection direction)
            {
                return direction switch
                {
                    BlockDirection.North => new Vector3Int(0, 0, 1),
                    BlockDirection.East => new Vector3Int(1, 0, 0),
                    BlockDirection.South => new Vector3Int(0, 0, -1),
                    BlockDirection.West => new Vector3Int(-1, 0, 0),
                    _ => Vector3Int.zero,
                };
            }

            #endregion
        }
    }
}
