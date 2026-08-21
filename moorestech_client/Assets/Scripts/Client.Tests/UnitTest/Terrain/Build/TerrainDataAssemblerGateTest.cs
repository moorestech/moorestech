using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Build;
using Game.MapGeneration.Cache;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UnitTest.Terrain.Build
{
    /// <summary>
    ///     generateHeightmap / generateTexture がTerrainDataへの適用だけを切ることを検証する。
    ///     器の寸法は移植元と違い常に設定し、テクスチャOFFではalphamapに一切触れないことをnull入力で固定する
    ///     Verifies generateHeightmap and generateTexture gate only what reaches the TerrainData: the terrain's own
    ///     dimensions are always set unlike the source, and a null input pins that the alphamap is untouched when texture is off
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
        public IEnumerator AppliesTheHeightsWhenTheHeightmapFlagIsOn()
        {
            yield return Assemble(CreateConfig(true, true));

            var appliedHeights = _terrainData.GetHeights(0, 0, Resolution, Resolution);
            Assert.That(appliedHeights[2, 2], Is.EqualTo(NormalizedHeight).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator LeavesTheTerrainFlatWhenTheHeightmapFlagIsOff()
        {
            yield return Assemble(CreateConfig(false, true));

            // 平坦のままであることだけがSetHeightsを通っていない証拠。器の寸法は落ちてはならない
            // Staying flat is the only evidence SetHeights was skipped, and the terrain's own dimensions must survive it
            var appliedHeights = _terrainData.GetHeights(0, 0, Resolution, Resolution);
            Assert.That(appliedHeights[2, 2], Is.EqualTo(0f).Within(0.001f));
            Assert.That(_terrainData.heightmapResolution, Is.EqualTo(Resolution));
            Assert.That(_terrainData.size, Is.EqualTo(new Vector3(TerrainWidth, TerrainHeight, TerrainWidth)));
        }

        [UnityTest]
        public IEnumerator AppliesTheSplatmapWhenTheTextureFlagIsOn()
        {
            yield return Assemble(CreateConfig(true, true));

            Assert.That(_terrainData.terrainLayers.Length, Is.EqualTo(LayerCount));
            Assert.That(_terrainData.alphamapResolution, Is.EqualTo(AlphamapResolution));

            var appliedAlphamap = _terrainData.GetAlphamaps(0, 0, AlphamapResolution, AlphamapResolution);
            Assert.That(appliedAlphamap[1, 1, 0], Is.EqualTo(1f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator LeavesTheDefaultAlphamapWhenTheTextureFlagIsOff()
        {
            yield return Assemble(CreateConfig(true, false));

            // レイヤーが1本も載らないことが、alphamapへ触れていない唯一の観測点になる
            // No layer being mounted is the single observable telling the alphamap was never touched
            Assert.That(_terrainData.terrainLayers, Is.Empty);
            Assert.That(_terrainData.alphamapResolution, Is.Not.EqualTo(AlphamapResolution));
        }

        private IEnumerator Assemble(TerrainGenerationConfig config)
        {
            // テクスチャOFFではproviderがalphamapをnullで返す。本番と同じ形を渡してassemblerがそこへ触れないことを固定する
            // With the texture off the provider hands back a null alphamap, so production's own shape goes in and pins that the assembler never touches it
            var tileVisual = new TerrainTileVisual(config.generateTexture ? CreateAlphamap() : null, new int[0][,]);
            var assembleTask = TerrainDataAssembler.AssembleAsync(
                config, CreateHeights(), tileVisual,
                new List<DetailPrototype>(), _terrainLayers);

            yield return assembleTask.ToCoroutine(terrainData => _terrainData = terrainData);
        }

        private static TerrainGenerationConfig CreateConfig(bool generateHeightmap, bool generateTexture)
        {
            return new TerrainGenerationConfig
            {
                overrideResolution = Resolution,
                terrainWidth = TerrainWidth,
                terrainLength = TerrainWidth,
                terrainHeight = TerrainHeight,
                generateHeightmap = generateHeightmap,
                generateTexture = generateTexture,
            };
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
        private static float[,,] CreateAlphamap()
        {
            var alphamap = new float[AlphamapResolution, AlphamapResolution, LayerCount];
            for (var z = 0; z < AlphamapResolution; z++)
            for (var x = 0; x < AlphamapResolution; x++)
                alphamap[z, x, 0] = 1f;

            return alphamap;
        }
    }
}
