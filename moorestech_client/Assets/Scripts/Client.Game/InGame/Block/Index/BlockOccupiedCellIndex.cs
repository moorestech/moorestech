using System.Collections.Generic;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.Block.Index
{
    public sealed class BlockOccupiedCellIndex
    {
        // チャンクは 16×16×16 セル。セル座標を右へ 4 ビットずらすとチャンク座標になる（負の座標も切り捨て）
        // A chunk is 16x16x16 cells; shifting a cell coordinate right by 4 bits gives its chunk (floors negatives too)
        public const int ChunkShift = 4;

        private readonly Dictionary<Vector3Int, BlockPositionInfo> _blockByCell = new();
        private readonly Dictionary<Vector3Int, int> _occupiedCountByChunk = new();
        private bool _hasBounds;
        private Vector3Int _minCell;
        private Vector3Int _maxCell;

        public void Register(BlockPositionInfo posInfo)
        {
            // 占有する全セルをこのブロックに結ぶ
            // Bind every occupied cell to this block
            var min = posInfo.MinPos;
            var max = posInfo.MaxPos;
            for (var x = min.x; x <= max.x; x++)
            for (var y = min.y; y <= max.y; y++)
            for (var z = min.z; z <= max.z; z++)
            {
                var cell = new Vector3Int(x, y, z);
                if (!_blockByCell.ContainsKey(cell)) AddChunkCount(cell, 1);
                _blockByCell[cell] = posInfo;
            }

            // 外接直方体は広げるだけで縮めない
            // The bounding box only grows and never shrinks
            _minCell = _hasBounds ? Vector3Int.Min(_minCell, min) : min;
            _maxCell = _hasBounds ? Vector3Int.Max(_maxCell, max) : max;
            _hasBounds = true;
        }

        public void Remove(BlockPositionInfo posInfo)
        {
            // 同じ参照に結ばれたセルだけを外し、後から置かれた別ブロックを消さない
            // Clear only cells bound to this exact reference so a later block is kept
            var min = posInfo.MinPos;
            var max = posInfo.MaxPos;
            for (var x = min.x; x <= max.x; x++)
            for (var y = min.y; y <= max.y; y++)
            for (var z = min.z; z <= max.z; z++)
            {
                var cell = new Vector3Int(x, y, z);
                if (!_blockByCell.TryGetValue(cell, out var owner) || !ReferenceEquals(owner, posInfo)) continue;
                _blockByCell.Remove(cell);
                AddChunkCount(cell, -1);
            }
        }

        public bool TryGetBlockAt(Vector3Int cell, out BlockPositionInfo posInfo)
        {
            return _blockByCell.TryGetValue(cell, out posInfo);
        }

        public bool IsChunkEmpty(Vector3Int cell)
        {
            // セルを含むチャンクに占有セルが1つも無いか
            // Whether the chunk containing this cell has no occupied cell
            return !_occupiedCountByChunk.ContainsKey(ChunkOf(cell));
        }

        public bool TryGetBounds(out Vector3Int minCell, out Vector3Int maxCell)
        {
            minCell = _minCell;
            maxCell = _maxCell;
            return _hasBounds;
        }

        private void AddChunkCount(Vector3Int cell, int delta)
        {
            // 数が 0 になったチャンクは辞書から外し、空かどうかをキーの有無で引けるようにする
            // Drop chunks whose count reaches zero so emptiness is a key lookup
            var chunk = ChunkOf(cell);
            _occupiedCountByChunk.TryGetValue(chunk, out var count);
            count += delta;
            if (count == 0) _occupiedCountByChunk.Remove(chunk);
            else _occupiedCountByChunk[chunk] = count;
        }

        private static Vector3Int ChunkOf(Vector3Int cell)
        {
            return new Vector3Int(cell.x >> ChunkShift, cell.y >> ChunkShift, cell.z >> ChunkShift);
        }
    }
}
