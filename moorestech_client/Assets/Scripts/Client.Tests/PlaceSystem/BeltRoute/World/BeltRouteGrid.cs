using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute.World
{
    // テスト用の世界。有限の箱で、列ごとの地形の下限・占有・坂の真上・入出力辺を持つ。箱の外は占有扱いで探索が外へ出ない
    // A test world: a finite box holding per-column terrain floors, occupancy, slope heads and port edges; outside the box counts as occupied so the search never leaves it
    // 面ビットの立て方は本番と同じ BeltRouteFaceBits.Mark
    // Face bits are raised by the same BeltRouteFaceBits.Mark as production
    public sealed class BeltRouteGrid : IBeltRouteWorld, IBeltRouteEdgeSink
    {
        private const int NoGround = int.MinValue;

        public readonly Vector3Int Min;
        public readonly Vector3Int Size;

        private readonly int[] _columnFloorY;
        private readonly HashSet<Vector3Int> _blockCells = new();
        private readonly HashSet<Vector3Int> _slopeHeads = new();
        private readonly Dictionary<Vector3Int, int> _outputFaceBits = new();
        private readonly Dictionary<Vector3Int, int> _inputFaceBits = new();

        public BeltRouteGrid(Vector3Int min, Vector3Int size)
        {
            Min = min;
            Size = size;
            _columnFloorY = new int[size.x * size.z];
            for (var i = 0; i < _columnFloorY.Length; i++) _columnFloorY[i] = NoGround;
        }

        public bool Contains(Vector3Int cell)
        {
            return Min.x <= cell.x && cell.x < Min.x + Size.x &&
                   Min.y <= cell.y && cell.y < Min.y + Size.y &&
                   Min.z <= cell.z && cell.z < Min.z + Size.z;
        }

        public void SetColumnFloor(int x, int z, int floorY) => _columnFloorY[(z - Min.z) * Size.x + (x - Min.x)] = floorY;
        public void MarkBlockCell(Vector3Int cell) => _blockCells.Add(cell);
        public void MarkSlopeHead(Vector3Int cell) => _slopeHeads.Add(cell);
        public void AddOutputEdge(BeltRouteEdge edge) => BeltRouteFaceBits.Mark(edge, _outputFaceBits);
        public void AddInputEdge(BeltRouteEdge edge) => BeltRouteFaceBits.Mark(edge, _inputFaceBits);

        public bool IsBlockedByBlock(Vector3Int cell) => !Contains(cell) || _blockCells.Contains(cell);

        public bool IsBlockedByTerrain(Vector3Int cell)
        {
            if (!Contains(cell)) return false;
            var floor = _columnFloorY[(cell.z - Min.z) * Size.x + (cell.x - Min.x)];
            return floor != NoGround && cell.y < floor;
        }

        public bool IsSlopeHead(Vector3Int cell) => _slopeHeads.Contains(cell);
        public int OutputFaceBits(Vector3Int cell) => _outputFaceBits.TryGetValue(cell, out var bits) ? bits : 0;
        public int InputFaceBits(Vector3Int cell) => _inputFaceBits.TryGetValue(cell, out var bits) ? bits : 0;
    }
}
