using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World
{
    /// <summary>
    ///     本番の世界。占有・坂の真上・入出力辺はブロックが変わるたびに全ブロックから作り直し、地形は列ごとに初めて触れたときだけ測って覚える
    ///     The live world: occupancy, slope heads and port edges are rebuilt from every block whenever blocks change; terrain is probed once per column on first touch
    ///     箱を持たないので範囲の上限が無い（ADR 0099）
    ///     There is no box, so there is no range limit (ADR 0099)
    /// </summary>
    public sealed class BeltRouteLiveWorld : IBeltRouteWorld, IBeltRouteEdgeSink
    {
        private const int NoGround = int.MinValue;

        private readonly BlockGameObjectDataStore _blocks;
        private readonly Dictionary<Vector2Int, int> _floors = new();
        private readonly HashSet<Vector3Int> _blockCells = new();
        private readonly HashSet<Vector3Int> _slopeHeads = new();
        private readonly Dictionary<Vector3Int, int> _outputFaceBits = new();
        private readonly Dictionary<Vector3Int, int> _inputFaceBits = new();

        public BeltRouteLiveWorld(BlockGameObjectDataStore blocks)
        {
            _blocks = blocks;
            Rebuild();
        }

        // ブロックが置かれた・撤去されたら呼ぶ。地形は変わらないので覚えたまま
        // Called when a block is placed or removed; terrain does not change, so its memory is kept
        public void Rebuild()
        {
            _blockCells.Clear();
            _slopeHeads.Clear();
            _outputFaceBits.Clear();
            _inputFaceBits.Clear();
            foreach (var block in _blocks.BlockGameObjectDictionary.Values)
            {
                MarkFootprint(block);
                BeltRouteBlockEdgeScanner.Write(block, this);
            }
        }

        public bool IsBlockedByTerrain(Vector3Int cell)
        {
            var floor = FloorOf(cell.x, cell.z);
            return floor != NoGround && cell.y < floor;
        }

        public bool IsBlockedByBlock(Vector3Int cell) => _blockCells.Contains(cell);
        public bool IsSlopeHead(Vector3Int cell) => _slopeHeads.Contains(cell);
        public int OutputFaceBits(Vector3Int cell) => _outputFaceBits.TryGetValue(cell, out var bits) ? bits : 0;
        public int InputFaceBits(Vector3Int cell) => _inputFaceBits.TryGetValue(cell, out var bits) ? bits : 0;
        public void AddOutputEdge(BeltRouteEdge edge) => BeltRouteFaceBits.Mark(edge, _outputFaceBits);
        public void AddInputEdge(BeltRouteEdge edge) => BeltRouteFaceBits.Mark(edge, _inputFaceBits);
        public void MarkSlopeHead(Vector3Int cell) => _slopeHeads.Add(cell);

        // 初めて触れた列だけ地形を測る。沈み許容は通常設置と同じ（PlacementGroundCellResolver）
        // Probes terrain only on a column's first touch; the sink allowance matches normal placement (PlacementGroundCellResolver)
        private int FloorOf(int x, int z)
        {
            var column = new Vector2Int(x, z);
            if (_floors.TryGetValue(column, out var floor)) return floor;
            // silent-ok: 地表が無い列は下限なし/ プレビューでそのセルが埋まらないことが見える
            floor = GroundHeightProbe.TryGetFootprintMaxGroundHeight(new Vector3Int(x, 0, z), BlockDirection.North, Vector3Int.one, out var groundMax)
                ? PlacementGroundCellResolver.ResolveCellY(groundMax, 0)
                : NoGround;
            _floors[column] = floor;
            return floor;
        }

        private void MarkFootprint(BlockGameObject block)
        {
            var min = block.BlockPosInfo.MinPos;
            var max = block.BlockPosInfo.MaxPos;
            for (var x = min.x; x <= max.x; x++)
            for (var y = min.y; y <= max.y; y++)
            for (var z = min.z; z <= max.z; z++)
                _blockCells.Add(new Vector3Int(x, y, z));
        }
    }
}
