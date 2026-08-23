using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Build;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Facade;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UnitTest.Terrain.Build
{
    /// <summary>
    ///     BakedTerrainTileの中身のみが適用可否を決めることを検証
    ///     高さはファサードが常に持つ値なので必ず適用され、器の寸法もWorldTerrainLayoutから常に設定される
    ///     Verifies only the baked tile's own contents gate what reaches the TerrainData
    ///     Heights always apply since the facade always carries them, and dimensions always come from WorldTerrainLayout
    /// </summary>
    public class TerrainDataAssemblerGateTest
    {
        // Unityはheightmapを33未満へ落とせない。既定の513と区別できる最小値がこれ
        // Unity refuses a heightmap below 33, which is the smallest value still distinguishable from the default 513
        private const int Resolution = 33;

        // Unityはalphamapを16未満へ落とせない。4を渡すと黙って16へ切り上げられ寸法の照合が空振りする
        // Unity refuses an alphamap below 16: passing 4 is silently rounded up and the dimension check watches nothing
        private const int AlphamapResolution = 16;
        private const int LayerCount = 2;
        private const float TerrainWidth = 100f;
        private const float TerrainHeight = 50f;
        private const float NormalizedHeight = 0.5f;

        private TerrainData _terrainData;
        private TerrainLayer[] _terrainLayers;

        [SetUp]
        public void SetUp()
        {
            _terrainLayers = new TerrainLayer[LayerCount];
            for (var layer = 0; layer < LayerCount; layer++) _terrainLayers[layer] = new TerrainLayer();
        }

        [TearDown]
        public void TearDown()
        {
            if (_terrainData != null) Object.DestroyImmediate(_terrainData);
            foreach (var terrainLayer in _terrainLayers) Object.DestroyImmediate(terrainLayer);
        }

        [UnityTest]
        public IEnumerator AppliesTheHeightsAlways()
        {
            yield return Assemble(CreateAlphamapPlanes(), AlphamapResolution, LayerCount);

            var appliedHeights = _terrainData.GetHeights(0, 0, Resolution, Resolution);
            Assert.That(appliedHeights[2, 2], Is.EqualTo(NormalizedHeight).Within(0.001f));
            Assert.That(_terrainData.heightmapResolution, Is.EqualTo(Resolution));
            Assert.That(_terrainData.size, Is.EqualTo(new Vector3(TerrainWidth, TerrainHeight, TerrainWidth)));
        }

        [UnityTest]
        public IEnumerator AppliesTheSplatmapWhenTheAlphamapIsPresent()
        {
            yield return Assemble(CreateAlphamapPlanes(), AlphamapResolution, LayerCount);

            Assert.That(_terrainData.terrainLayers.Length, Is.EqualTo(LayerCount));
            Assert.That(_terrainData.alphamapResolution, Is.EqualTo(AlphamapResolution));

            var appliedAlphamap = _terrainData.GetAlphamaps(0, 0, AlphamapResolution, AlphamapResolution);
            Assert.That(appliedAlphamap[1, 1, 0], Is.EqualTo(1f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator LeavesTheDefaultAlphamapWhenNoLayerWasBaked()
        {
            yield return Assemble(System.Array.Empty<byte[]>(), 0, 0);

            // レイヤーが1本も載らないことが、alphamapへ触れていない唯一の観測点になる
            // No layer being mounted is the single observable telling the alphamap was never touched
            Assert.That(_terrainData.terrainLayers, Is.Empty);
            Assert.That(_terrainData.alphamapResolution, Is.Not.EqualTo(AlphamapResolution));
        }

        [TestCase(1)]
        [TestCase(15)]
        [TestCase(17)]
        [TestCase(48)]
        public void RejectsDetailResolutionUnityWouldRound(int detailResolution)
        {
            var task = AssembleWithDetailMaps(new List<int[,]> { new int[detailResolution, detailResolution] });

            Assert.Throws<System.InvalidOperationException>(() => task.GetAwaiter().GetResult());
        }

        [Test]
        public void RejectsDetailMapsWithDifferentShapes()
        {
            var task = AssembleWithDetailMaps(new List<int[,]> { new int[16, 16], new int[16, 15] });

            Assert.Throws<System.InvalidOperationException>(() => task.GetAwaiter().GetResult());
        }

        private IEnumerator Assemble(IReadOnlyList<byte[]> alphamapPlanes, int alphamapResolution, int alphamapLayerCount)
        {
            var layout = WorldTerrainLayout.CreateTileMaps(
                new List<(int TileX, int TileZ)> { (0, 0) }, new Vector3(TerrainWidth, TerrainHeight, TerrainWidth), Resolution,
                new List<string>(), new List<DetailPrototypeSpec>());
            var tile = new BakedTerrainTile(
                Vector3.zero, CreateHeights(), alphamapPlanes, alphamapResolution, alphamapLayerCount, new List<int[,]>());

            var assembleTask = TerrainDataAssembler.AssembleAsync(layout, tile, new List<DetailPrototype>(), _terrainLayers);

            yield return assembleTask.ToCoroutine(terrainData => _terrainData = terrainData);
        }

        private static UniTask<TerrainData> AssembleWithDetailMaps(IReadOnlyList<int[,]> detailMaps)
        {
            var layout = WorldTerrainLayout.CreateTileMaps(
                new List<(int TileX, int TileZ)> { (0, 0) }, new Vector3(TerrainWidth, TerrainHeight, TerrainWidth), Resolution,
                new List<string>(), new List<DetailPrototypeSpec>());
            var tile = new BakedTerrainTile(
                Vector3.zero, CreateHeights(), System.Array.Empty<byte[]>(), 0, 0, detailMaps);
            var prototypes = new List<DetailPrototype>();
            for (var index = 0; index < detailMaps.Count; index++) prototypes.Add(new DetailPrototype());

            return TerrainDataAssembler.AssembleAsync(layout, tile, prototypes, System.Array.Empty<TerrainLayer>());
        }

        private static float[,] CreateHeights()
        {
            var heights = new float[Resolution, Resolution];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
                heights[z, x] = NormalizedHeight;

            return heights;
        }

        // 0番レイヤーだけに全重みを寄せる。適用されたかどうかを1画素の重みで見分けられる形
        // All weight goes to layer 0 so a single pixel's weight tells whether the apply happened
        private static IReadOnlyList<byte[]> CreateAlphamapPlanes()
        {
            // 平面は[z][x][rgba]の生バイト。焼き側の変換に依存させず、この並びを直に組んで期待値を固定する
            // A plane is raw [z][x][rgba] bytes; building the order here rather than through the baker pins the expectation itself
            var plane = new byte[AlphamapResolution * AlphamapResolution * 4];
            for (var pixel = 0; pixel < AlphamapResolution * AlphamapResolution; pixel++)
                plane[pixel * 4] = byte.MaxValue;

            return new[] { plane };
        }
    }
}
