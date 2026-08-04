using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build
{
    /// <summary>
    ///     タイル格子上のTerrainへ隣接関係を張る。UnityのLODシーム処理はこの参照が無いとタイル境界に段差を出す
    ///     Links neighbouring Terrains across the tile grid; without these references Unity's LOD seam handling cracks the tile borders
    /// </summary>
    public static class TerrainNeighborLinker
    {
        public static void Link(IReadOnlyDictionary<Vector2Int, UnityEngine.Terrain> terrainsByTileCoordinate)
        {
            foreach (var terrainByTileCoordinate in terrainsByTileCoordinate)
            {
                var coordinate = terrainByTileCoordinate.Key;

                // SetNeighborsの引数順は左・上・右・下。上はz+側で、タイル座標のy+と一致する
                // SetNeighbors takes left, top, right, bottom; top is the +z side, which matches +y in tile coordinates
                terrainByTileCoordinate.Value.SetNeighbors(
                    FindOrNull(terrainsByTileCoordinate, coordinate + Vector2Int.left),
                    FindOrNull(terrainsByTileCoordinate, coordinate + Vector2Int.up),
                    FindOrNull(terrainsByTileCoordinate, coordinate + Vector2Int.right),
                    FindOrNull(terrainsByTileCoordinate, coordinate + Vector2Int.down));
                terrainByTileCoordinate.Value.Flush();
            }
        }

        private static UnityEngine.Terrain FindOrNull(
            IReadOnlyDictionary<Vector2Int, UnityEngine.Terrain> terrainsByTileCoordinate, Vector2Int coordinate)
        {
            return terrainsByTileCoordinate.TryGetValue(coordinate, out var terrain) ? terrain : null;
        }
    }
}
