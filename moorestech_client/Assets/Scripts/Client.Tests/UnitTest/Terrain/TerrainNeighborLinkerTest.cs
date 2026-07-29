using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Build;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UnitTest.Terrain
{
    /// <summary>
    ///     タイル格子の隣接接続を検証する。SetNeighborsの引数順(左・上・右・下)は取り違えても例外にならず、
    ///     タイル境界のLODシームという見た目でしか現れない
    ///     Verifies neighbour linking across the tile grid. SetNeighbors' left/top/right/bottom order never throws
    ///     when swapped and surfaces only as an LOD seam at the tile borders
    /// </summary>
    public class TerrainNeighborLinkerTest
    {
        private readonly List<GameObject> _createdTerrainObjects = new();

        [TearDown]
        public void TearDown()
        {
            // EditModeなので開いているシーンに残さないよう即時破棄する
            // This runs in EditMode, so destroy immediately to leave nothing behind in the open scene
            foreach (var terrainObject in _createdTerrainObjects)
            {
                if (terrainObject == null) continue;
                var terrainData = terrainObject.GetComponent<UnityEngine.Terrain>().terrainData;
                Object.DestroyImmediate(terrainObject);
                Object.DestroyImmediate(terrainData);
            }

            _createdTerrainObjects.Clear();
        }

        [Test]
        public void LinksEachAxisToTheTileOnThatSide()
        {
            // 2x2ならどのタイルもx方向とz方向に1枚ずつ隣を持ち、4方向すべての対応が同時に決まる
            // In a 2x2 every tile has exactly one neighbour per axis, pinning all four directions at once
            var terrainsByTileCoordinate = CreateGrid(2, 2);

            TerrainNeighborLinker.Link(terrainsByTileCoordinate);

            var origin = terrainsByTileCoordinate[new Vector2Int(0, 0)];
            Assert.That(origin.rightNeighbor, Is.SameAs(terrainsByTileCoordinate[new Vector2Int(1, 0)]), "x+ が right");
            Assert.That(origin.topNeighbor, Is.SameAs(terrainsByTileCoordinate[new Vector2Int(0, 1)]), "z+ が top");
            Assert.That(origin.leftNeighbor, Is.Null, "x- は格子外");
            Assert.That(origin.bottomNeighbor, Is.Null, "z- は格子外");

            var far = terrainsByTileCoordinate[new Vector2Int(1, 1)];
            Assert.That(far.leftNeighbor, Is.SameAs(terrainsByTileCoordinate[new Vector2Int(0, 1)]), "x- が left");
            Assert.That(far.bottomNeighbor, Is.SameAs(terrainsByTileCoordinate[new Vector2Int(1, 0)]), "z- が bottom");
            Assert.That(far.rightNeighbor, Is.Null);
            Assert.That(far.topNeighbor, Is.Null);
        }

        [Test]
        public void LeavesASingleTileWithoutNeighbours()
        {
            // 現状のサーバーは常に1タイルしか吐かない。全nullへ無害に縮退することを固定する
            // The server currently emits a single tile, so pin that this degenerates harmlessly to all-null
            var terrainsByTileCoordinate = CreateGrid(1, 1);

            TerrainNeighborLinker.Link(terrainsByTileCoordinate);

            var only = terrainsByTileCoordinate[new Vector2Int(0, 0)];
            Assert.That(only.leftNeighbor, Is.Null);
            Assert.That(only.topNeighbor, Is.Null);
            Assert.That(only.rightNeighbor, Is.Null);
            Assert.That(only.bottomNeighbor, Is.Null);
        }

        private Dictionary<Vector2Int, UnityEngine.Terrain> CreateGrid(int tilesX, int tilesZ)
        {
            var terrainsByTileCoordinate = new Dictionary<Vector2Int, UnityEngine.Terrain>();

            for (var tileZ = 0; tileZ < tilesZ; tileZ++)
            for (var tileX = 0; tileX < tilesX; tileX++)
            {
                var terrainObject = new GameObject($"TerrainNeighborLinkerTest_{tileX}_{tileZ}");
                var terrain = terrainObject.AddComponent<UnityEngine.Terrain>();

                // FlushがterrainDataを触るので実体を与える
                // Flush touches terrainData, so give it a real instance
                terrain.terrainData = new TerrainData();

                _createdTerrainObjects.Add(terrainObject);
                terrainsByTileCoordinate[new Vector2Int(tileX, tileZ)] = terrain;
            }

            return terrainsByTileCoordinate;
        }
    }
}
