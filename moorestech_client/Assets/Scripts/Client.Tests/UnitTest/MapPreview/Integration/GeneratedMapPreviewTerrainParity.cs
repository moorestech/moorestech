using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Environment.Terrain.Assets;
using Client.Game.InGame.Environment.Terrain.Build;
using Client.MapScene.Editor;
using Game.MapGeneration.Facade;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UnitTest.MapPreview.Integration
{
    internal static class GeneratedMapPreviewTerrainParity
    {
        internal static IEnumerator AssertMatches(GameObject root, TiledTerrainSession session)
        {
            var layout = session.Layout;
            var assets = new EditorTerrainAssetLoader();
            var layers = TerrainLayerAssetLoader.LoadAsync(layout.TextureLayerAddresses, assets, CancellationToken.None).GetAwaiter().GetResult();
            var material = TerrainMaterialAssetLoader.LoadAsync(assets, CancellationToken.None).GetAwaiter().GetResult();
            var prototypes = DetailPrototypeAssetResolver.ResolveAsync(layout.DetailPrototypes, assets, CancellationToken.None).GetAwaiter().GetResult();
            var byCoordinate = new Dictionary<Vector2Int, UnityEngine.Terrain>();
            var terrains = root.GetComponentsInChildren<UnityEngine.Terrain>();
            Assert.That(terrains.Length, Is.EqualTo(layout.TileCoordinates.Count));
            long detailCells = 0;
            foreach (var (x, z) in layout.TileCoordinates)
            {
                var tile = session.BakeTile(x, z);
                var terrain = terrains.Single(t => t.transform.position == tile.ScenePosition);
                byCoordinate.Add(new Vector2Int(x, z), terrain);
                var data = terrain.terrainData;
                Assert.That(data.size, Is.EqualTo(layout.TileSize));
                Assert.That(data.heightmapResolution, Is.EqualTo(layout.HeightmapResolution));
                Assert.That(data.terrainLayers, Is.EqualTo(layers));
                Assert.That(terrain.materialTemplate, Is.SameAs(material));
                Assert.That(terrain.materialTemplate.shader, Is.SameAs(material.shader));
                Assert.That(terrain.GetComponent<TerrainCollider>().terrainData, Is.SameAs(data));
                Assert.That(terrain.detailObjectDistance, Is.EqualTo(layout.DetailObjectDistance));
                Assert.That(terrain.detailObjectDensity, Is.EqualTo(layout.DetailObjectDensity));

                // 高さは[z,x]を保持し、端と非対称の内部点で転置も検出する
                // Preserve [z,x] indexing and include asymmetric interior samples to detect transposition
                var edge = layout.HeightmapResolution - 1;
                foreach (var row in new[] { 0, edge / 3, edge / 2, edge })
                    foreach (var column in new[] { 0, edge / 4, edge / 2, edge })
                        Assert.That(data.GetHeights(column, row, 1, 1)[0, 0], Is.EqualTo(tile.DisplayHeights[row, column]).Within(1f / 32767f), $"Height {x},{z} [{row},{column}]");
                Assert.That(data.detailPrototypes.Length, Is.EqualTo(prototypes.Count));
                for (var index = 0; index < tile.DetailMaps.Count; index++)
                {
                    Assert.That(data.detailPrototypes[index].prototype, Is.SameAs(prototypes[index].prototype));
                    Assert.That(data.detailPrototypes[index].prototypeTexture, Is.SameAs(prototypes[index].prototypeTexture));
                    var expected = tile.DetailMaps[index];
                    var actual = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, index);
                    Assert.That(actual.GetLength(0), Is.EqualTo(expected.GetLength(0)));
                    Assert.That(actual.GetLength(1), Is.EqualTo(expected.GetLength(1)));
                    // 密度は全セルを整数のまま照合し、代表値や画像で代用しない
                    // Compare every density cell as an integer rather than substituting samples or screenshots
                    for (var row = 0; row < expected.GetLength(0); row++)
                        for (var column = 0; column < expected.GetLength(1); column++)
                            if (actual[row, column] != expected[row, column])
                                Assert.Fail($"Detail {x},{z} layer {index} [{row},{column}]: {actual[row, column]} != {expected[row, column]}");
                    detailCells += actual.LongLength;
                }
                yield return null;
            }
            foreach (var pair in byCoordinate)
            {
                Assert.That(pair.Value.leftNeighbor, Is.SameAs(Neighbor(pair.Key + Vector2Int.left)));
                Assert.That(pair.Value.topNeighbor, Is.SameAs(Neighbor(pair.Key + Vector2Int.up)));
                Assert.That(pair.Value.rightNeighbor, Is.SameAs(Neighbor(pair.Key + Vector2Int.right)));
                Assert.That(pair.Value.bottomNeighbor, Is.SameAs(Neighbor(pair.Key + Vector2Int.down)));
            }
            Debug.Log($"[GeneratedMapPreviewIntegration] {terrains.Length} terrains, {detailCells} detail cells, all neighbor edges, {layers.Length} ordered layers and shader {material.shader.name} match.");

            #region Internal

            UnityEngine.Terrain Neighbor(Vector2Int coordinate) => byCoordinate.GetValueOrDefault(coordinate);

            #endregion
        }
    }
}
