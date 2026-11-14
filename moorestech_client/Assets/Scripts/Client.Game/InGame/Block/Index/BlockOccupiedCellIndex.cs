using System.Collections.Generic;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.Block.Index
{
    public sealed class BlockOccupiedCellIndex
    {
        private readonly Dictionary<Vector3Int, BlockPositionInfo> _blockByCell = new();
        private bool _hasBounds;
        private Vector3Int _minCell;
        private Vector3Int _maxCell;

        // 照準の走査が空の区画を飛ばすための八分木。ビットが立つセルと辞書にあるセルは常に一致させる
        // Octree the aim walk uses to skip empty regions; its set bits always match the cells in the dictionary
        public OccupiedCellOctree Octree { get; private set; } = new();

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
                if (!_blockByCell.ContainsKey(cell)) Octree.Add(cell);
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
                Octree.Remove(cell);
            }
        }

        public bool TryGetBlockAt(Vector3Int cell, out BlockPositionInfo posInfo)
        {
            return _blockByCell.TryGetValue(cell, out posInfo);
        }

        public bool TryGetBounds(out Vector3Int minCell, out Vector3Int maxCell)
        {
            minCell = _minCell;
            maxCell = _maxCell;
            return _hasBounds;
        }
    }
}
