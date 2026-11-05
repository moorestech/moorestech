using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route
{
    /// <summary>
    ///     始点で固定した探索箱と、その中の障害情報（地形の下限・既存ブロック・既存坂の真上・既存の入出力辺）
    ///     The search box fixed at the start, with its obstacles: terrain floors, existing blocks, existing slope heads and existing port edges
    /// </summary>
    public sealed class BeltRouteGrid
    {
        public const int HorizontalSize = 64;

        // 始点の高い側に足す余白。上限は地形最高点と始点の高い方＋この値
        // Headroom added above the higher of the terrain max and the start; the upper bound is that height plus this margin
        public const int HeadroomAboveMax = 3;

        private const int NoGround = int.MinValue;

        public readonly Vector3Int Min;
        public readonly Vector3Int Size;

        private readonly int[] _columnFloorY;
        private readonly bool[] _blockCells;
        private readonly bool[] _slopeHeadCells;
        private readonly HashSet<BeltRouteEdge> _outputEdges = new();
        private readonly HashSet<BeltRouteEdge> _inputEdges = new();

        public BeltRouteGrid(Vector3Int min, Vector3Int size)
        {
            Min = min;
            Size = size;
            _columnFloorY = new int[size.x * size.z];
            for (var i = 0; i < _columnFloorY.Length; i++) _columnFloorY[i] = NoGround;
            _blockCells = new bool[CellCount];
            _slopeHeadCells = new bool[CellCount];
        }

        public int CellCount => Size.x * Size.y * Size.z;
        public int MaxY => Min.y + Size.y - 1;

        // 始点を中心にX64×Z64、Yは呼び出し側が地形と始点から決めた範囲
        // 64x64 horizontally around the start; the Y range is decided by the caller from terrain and the start
        public static BeltRouteGrid CreateAround(Vector3Int startCell, int minY, int maxY)
        {
            var min = new Vector3Int(startCell.x - HorizontalSize / 2, minY, startCell.z - HorizontalSize / 2);
            return new BeltRouteGrid(min, new Vector3Int(HorizontalSize, maxY - minY + 1, HorizontalSize));
        }

        public bool Contains(Vector3Int cell)
        {
            return Min.x <= cell.x && cell.x < Min.x + Size.x &&
                   Min.y <= cell.y && cell.y < Min.y + Size.y &&
                   Min.z <= cell.z && cell.z < Min.z + Size.z;
        }

        public bool ContainsColumn(int x, int z)
        {
            return Min.x <= x && x < Min.x + Size.x && Min.z <= z && z < Min.z + Size.z;
        }

        public int CellIndex(Vector3Int cell)
        {
            var local = cell - Min;
            return (local.y * Size.z + local.z) * Size.x + local.x;
        }

        public Vector3Int CellAt(int cellIndex)
        {
            var x = cellIndex % Size.x;
            var rest = cellIndex / Size.x;
            var z = rest % Size.z;
            var y = rest / Size.z;
            return Min + new Vector3Int(x, y, z);
        }

        // 地形の下限。これより下のセルは地面に埋まるので障害になる
        // Terrain floor; cells below it are buried in the ground and count as obstacles
        public void SetColumnFloor(int x, int z, int floorY)
        {
            _columnFloorY[ColumnIndex(x, z)] = floorY;
        }

        public void MarkBlockCell(Vector3Int cell)
        {
            if (Contains(cell)) _blockCells[CellIndex(cell)] = true;
        }

        public void MarkSlopeHead(Vector3Int cell)
        {
            if (Contains(cell)) _slopeHeadCells[CellIndex(cell)] = true;
        }

        public void AddOutputEdge(BeltRouteEdge edge) => _outputEdges.Add(edge);
        public void AddInputEdge(BeltRouteEdge edge) => _inputEdges.Add(edge);

        public bool IsBlockedByTerrain(Vector3Int cell)
        {
            var floor = _columnFloorY[ColumnIndex(cell.x, cell.z)];
            return floor != NoGround && cell.y < floor;
        }

        public bool IsBlockedByBlock(Vector3Int cell) => _blockCells[CellIndex(cell)];
        public bool IsSlopeHead(Vector3Int cell) => _slopeHeadCells[CellIndex(cell)];
        public bool HasOutputEdge(BeltRouteEdge edge) => _outputEdges.Contains(edge);
        public bool HasInputEdge(BeltRouteEdge edge) => _inputEdges.Contains(edge);

        private int ColumnIndex(int x, int z)
        {
            return (z - Min.z) * Size.x + (x - Min.x);
        }
    }
}
