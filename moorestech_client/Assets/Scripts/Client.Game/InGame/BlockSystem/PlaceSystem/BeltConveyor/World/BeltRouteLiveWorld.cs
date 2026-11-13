using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World
{
    /// <summary>
    ///     本番の世界。占有・辺は変化時に再構築、地形は列ごとに初回のみ測定
    ///     Live world: occupancy and edges rebuilt on change, terrain probed once per column
    ///     箱を持たないので範囲の上限が無い（ADR 0100）
    ///     There is no box, so there is no range limit (ADR 0100)
    /// </summary>
    public sealed class BeltRouteLiveWorld : IBeltRouteWorld, IBeltRouteEdgeSink
    {
        private const float NoGround = float.MinValue;

        private readonly BlockGameObjectDataStore _blocks;
        private readonly Dictionary<Vector2Int, float> _groundMaxHeights = new();
        private readonly HashSet<Vector3Int> _blockCells = new();
        private readonly HashSet<Vector3Int> _slopeHeads = new();
        private readonly Dictionary<Vector3Int, int> _outputFaceBits = new();
        private readonly Dictionary<Vector3Int, int> _inputFaceBits = new();

        public BeltRouteLiveWorld(BlockGameObjectDataStore blocks)
        {
            _blocks = blocks;
            Rebuild();
        }

        // ブロックの改訂番号が変わったとき設置システムが呼ぶ。地形は保持
        // Called by the place system when the block revision changed; terrain is kept
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

            #region Internal

            void MarkFootprint(BlockGameObject block)
            {
                var min = block.BlockPosInfo.MinPos;
                var max = block.BlockPosInfo.MaxPos;
                for (var x = min.x; x <= max.x; x++)
                for (var y = min.y; y <= max.y; y++)
                for (var z = min.z; z <= max.z; z++)
                    _blockCells.Add(new Vector3Int(x, y, z));
            }

            #endregion
        }

        // 埋まり判定は通常設置と同じPlacementTerrainSinkQueryへ委譲
        // Buried check delegates to PlacementTerrainSinkQuery, as normal placement
        public bool IsBlockedByTerrain(Vector3Int cell)
        {
            var groundMax = GroundMaxOf(cell.x, cell.z);
            return groundMax != NoGround && PlacementTerrainSinkQuery.IsSunkAbove(cell.y, groundMax);

            #region Internal

            // 初めて触れた列だけ地形を測る
            // Probes terrain only on a column's first touch
            float GroundMaxOf(int x, int z)
            {
                var column = new Vector2Int(x, z);
                if (_groundMaxHeights.TryGetValue(column, out var cached)) return cached;
                // silent-ok: 地表が無い列は下限なし（ADR 0081と同じ扱い）/ プレビューでそのセルが埋まらないことが見える
                // silent-ok: a column with no ground has no floor (the same treatment as ADR 0081); the preview shows that cell stays unburied
                var max = GroundHeightProbe.TryGetFootprintMaxGroundHeight(new Vector3Int(x, 0, z), BlockDirection.North, Vector3Int.one, out var probed) ? probed : NoGround;
                _groundMaxHeights[column] = max;
                return max;
            }

            #endregion
        }

        public bool IsBlockedByBlock(Vector3Int cell) => _blockCells.Contains(cell);
        public bool IsSlopeHead(Vector3Int cell) => _slopeHeads.Contains(cell);
        public int OutputFaceBits(Vector3Int cell) => _outputFaceBits.TryGetValue(cell, out var bits) ? bits : 0;
        public int InputFaceBits(Vector3Int cell) => _inputFaceBits.TryGetValue(cell, out var bits) ? bits : 0;
        public void AddOutputEdge(BeltRouteEdge edge) => BeltRouteFaceBits.Mark(edge, _outputFaceBits);
        public void AddInputEdge(BeltRouteEdge edge) => BeltRouteFaceBits.Mark(edge, _inputFaceBits);
        public void MarkSlopeHead(Vector3Int cell) => _slopeHeads.Add(cell);
    }
}
