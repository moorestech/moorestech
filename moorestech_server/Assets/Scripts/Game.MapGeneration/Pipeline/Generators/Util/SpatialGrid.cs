using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators.Util
{
    // O(1) 近傍検索用の空間グリッド。Tree/Object/Ore の距離制約に使う共有ユーティリティ。
    // Spatial grid for O(1) neighbor queries, shared for tree/object/ore distance constraints.
    public class SpatialGrid
    {
        readonly float _cellSize;
        readonly int _gridW, _gridH;
        readonly List<Vector2>[,] _cells;

        public SpatialGrid(float width, float height, float cellSize)
        {
            _cellSize = Mathf.Max(cellSize, 1f);
            _gridW = Mathf.CeilToInt(width / _cellSize);
            _gridH = Mathf.CeilToInt(height / _cellSize);
            _cells = new List<Vector2>[_gridW, _gridH];
        }

        public int Count { get; private set; }

        public void Add(float x, float y)
        {
            int gx = Mathf.Clamp(Mathf.FloorToInt(x / _cellSize), 0, _gridW - 1);
            int gy = Mathf.Clamp(Mathf.FloorToInt(y / _cellSize), 0, _gridH - 1);
            _cells[gx, gy] ??= new List<Vector2>();
            _cells[gx, gy].Add(new Vector2(x, y));
            Count++;
        }

        public bool HasNeighborWithin(float x, float y, float radius)
        {
            float radiusSq = radius * radius;
            int cx = Mathf.FloorToInt(x / _cellSize);
            int cy = Mathf.FloorToInt(y / _cellSize);
            int searchRadius = Mathf.CeilToInt(radius / _cellSize);

            for (int dx = -searchRadius; dx <= searchRadius; dx++)
            for (int dy = -searchRadius; dy <= searchRadius; dy++)
            {
                int gx = cx + dx, gy = cy + dy;
                if (gx < 0 || gx >= _gridW || gy < 0 || gy >= _gridH) continue;
                var cell = _cells[gx, gy];
                if (cell == null) continue;
                foreach (var p in cell)
                {
                    float distSq = (p.x - x) * (p.x - x) + (p.y - y) * (p.y - y);
                    if (distSq < radiusSq) return true;
                }
            }
            return false;
        }

        public List<Vector2> GetAllPoints()
        {
            var all = new List<Vector2>();
            for (int x = 0; x < _gridW; x++)
            for (int y = 0; y < _gridH; y++)
            {
                if (_cells[x, y] != null)
                    all.AddRange(_cells[x, y]);
            }
            return all;
        }

        public int CountNeighborsWithin(float x, float y, float radius)
        {
            float radiusSq = radius * radius;
            int cx = Mathf.FloorToInt(x / _cellSize);
            int cy = Mathf.FloorToInt(y / _cellSize);
            int searchRadius = Mathf.CeilToInt(radius / _cellSize);
            int count = 0;

            for (int dx = -searchRadius; dx <= searchRadius; dx++)
            for (int dy = -searchRadius; dy <= searchRadius; dy++)
            {
                int gx = cx + dx, gy = cy + dy;
                if (gx < 0 || gx >= _gridW || gy < 0 || gy >= _gridH) continue;
                var cell = _cells[gx, gy];
                if (cell == null) continue;
                foreach (var p in cell)
                {
                    float distSq = (p.x - x) * (p.x - x) + (p.y - y) * (p.y - y);
                    if (distSq < radiusSq) count++;
                }
            }
            return count;
        }

        public bool Remove(float x, float y)
        {
            int gx = Mathf.Clamp(Mathf.FloorToInt(x / _cellSize), 0, _gridW - 1);
            int gy = Mathf.Clamp(Mathf.FloorToInt(y / _cellSize), 0, _gridH - 1);
            var cell = _cells[gx, gy];
            if (cell == null) return false;
            for (int i = cell.Count - 1; i >= 0; i--)
            {
                if (Mathf.Approximately(cell[i].x, x) && Mathf.Approximately(cell[i].y, y))
                {
                    cell.RemoveAt(i);
                    Count--;
                    return true;
                }
            }
            return false;
        }

        public float FindMinDistance(float x, float y, float maxRadius)
        {
            float minDistSq = maxRadius * maxRadius;
            int cx = Mathf.FloorToInt(x / _cellSize);
            int cy = Mathf.FloorToInt(y / _cellSize);
            int searchCells = Mathf.CeilToInt(maxRadius / _cellSize);

            for (int dx = -searchCells; dx <= searchCells; dx++)
            for (int dy = -searchCells; dy <= searchCells; dy++)
            {
                int gx = cx + dx, gy = cy + dy;
                if (gx < 0 || gx >= _gridW || gy < 0 || gy >= _gridH) continue;
                var cell = _cells[gx, gy];
                if (cell == null) continue;
                foreach (var p in cell)
                {
                    float distSq = (p.x - x) * (p.x - x) + (p.y - y) * (p.y - y);
                    if (distSq < minDistSq) minDistSq = distSq;
                }
            }
            return Mathf.Sqrt(minDistSq);
        }

        // PlacementEntry 配列から SpatialGrid を構築する。cellSize<=0 で自動決定。
        // Build a SpatialGrid from placements; cellSize<=0 auto-selects the cell size.
        public static SpatialGrid FromPlacements(
            IList<PlacementEntry> entries, float terrainWidth, float terrainLength, float cellSize)
        {
            if (cellSize <= 0f) cellSize = Mathf.Max(terrainWidth / 50f, 5f);
            var grid = new SpatialGrid(terrainWidth, terrainLength, cellSize);
            if (entries == null) return grid;
            foreach (var e in entries)
                grid.Add(e.WorldPosition.x, e.WorldPosition.z);
            return grid;
        }
    }
}
