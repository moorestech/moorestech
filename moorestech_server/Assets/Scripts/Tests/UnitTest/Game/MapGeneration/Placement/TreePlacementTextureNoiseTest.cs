using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Tiling;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // 樹木配置の「ノイズタイプ None でもテクスチャ源があれば有効」というガードを、公開経路
    // TreePlacementGenerator.GenerateForBiome を通した配置数の差として固定する。
    // 移植元 TreePlacementGenerator.cs:335,339,549 の `|| noise.texture != null` に対応する。
    // Pins the tree-placement guard that keeps a noise active with noiseType None as long as a texture
    // source exists, observed as a placement-count difference through the public GenerateForBiome path.
    // Matches `|| noise.texture != null` at the source's TreePlacementGenerator.cs:335,339,549.
    public class TreePlacementTextureNoiseTest
    {
        private const int Resolution = 16;
        private const float TerrainSize = 200f;

        // 真っ黒なクラスタテクスチャはしきい値以下で全候補を棄却し、真っ白は素通しする。
        // A black cluster texture rejects every candidate below the threshold while a white one lets them through.
        [Test]
        public void ノイズタイプNoneでもテクスチャ源があればクラスタ判定が働く()
        {
            var black = CreateEntry();
            black.clusterNoise = UniformTextureNoise(0);
            Assert.AreEqual(0, PlaceCount(black), "真っ黒なクラスタテクスチャは全候補を棄却するべき");

            var white = CreateEntry();
            white.clusterNoise = UniformTextureNoise(255);
            Assert.Greater(PlaceCount(white), 0, "真っ白なクラスタテクスチャは通過するべき");
        }

        // clusterNoise2 のテクスチャ源も noise2Op の合成に加わる。白×黒は Multiply で 0、Max なら 1 のまま。
        // The clusterNoise2 texture also feeds noise2Op: white against black is 0 under Multiply, still 1 under Max.
        [Test]
        public void クラスタノイズ2のテクスチャ源もnoise2Opの合成に加わる()
        {
            var multiply = CreateEntry();
            multiply.clusterNoise = UniformTextureNoise(255);
            multiply.clusterNoise2 = UniformTextureNoise(0);
            multiply.noise2Op = NoiseOp.Multiply;
            Assert.AreEqual(0, PlaceCount(multiply), "白と黒のMultiplyは0になり全候補が棄却されるべき");

            var max = CreateEntry();
            max.clusterNoise = UniformTextureNoise(255);
            max.clusterNoise2 = UniformTextureNoise(0);
            max.noise2Op = NoiseOp.Max;
            Assert.Greater(PlaceCount(max), 0, "白と黒のMaxは1のままなので通過するべき");
        }

        // フィルタノイズの「源なし」は 0（加算の中立値）。テクスチャ源の有無がそのまま重み 1/0 の分岐になる。
        // "No source" for a filter noise means 0, the additive neutral, so its presence flips the weight between 1 and 0.
        [Test]
        public void ノイズタイプNoneでもテクスチャ源があればフィルタノイズを読む()
        {
            var withTexture = CreateEntry();
            withTexture.slopeFilter = SlopeFilterReadingNoise(UniformTextureNoise(255));
            Assert.Greater(PlaceCount(withTexture), 0, "テクスチャの1.0が傾斜へ足されて範囲に入るべき");

            var withoutTexture = CreateEntry();
            withoutTexture.slopeFilter = SlopeFilterReadingNoise(default);
            Assert.AreEqual(0, PlaceCount(withoutTexture), "源なしのノイズは0なので範囲外で全棄却されるべき");
        }

        // 完全な平地・全面マスクの1タイルへ1プロトタイプだけを配置し、配置数だけを観測する。
        // Place a single prototype on one perfectly flat, fully masked tile and observe only the placement count.
        private static int PlaceCount(TreePrototypeEntry entry)
        {
            var dims = new TerrainDimensions(TerrainSize, TerrainSize, 100f, 0f, 0f, Resolution, Resolution - 1, 0f, 0f, 1, 0f, 0f, 0, 0, 1, 1);
            var heights = new float[Resolution * Resolution];
            var mask = new bool[Resolution, Resolution];
            for (int z = 0; z < Resolution; z++)
            for (int x = 0; x < Resolution; x++)
                mask[z, x] = true;

            var treeConfig = new TreePlacementConfig { prototypes = new[] { entry } };
            return TreePlacementGenerator.GenerateForBiome(
                mask, heights, dims, treeConfig, new System.Random(1), noiseSeed: 1,
                new PlacementHaloChannel(), 0f).Count;
        }

        private static TreePrototypeEntry CreateEntry()
        {
            return new TreePrototypeEntry
            {
                mapObjectGuids = new[] { "11111111-0000-0000-0000-000000000001" },
                clusterNoiseThreshold = 0.3f,
            };
        }

        // 平地の傾斜0にノイズ値を足した結果で判定させる。テクスチャの 1.0 だけが範囲 [0.5,1.5] に入る。
        // Judges slope 0 plus the noise value, so only the texture's 1.0 lands inside the [0.5,1.5] range.
        private static PlacementFilter SlopeFilterReadingNoise(PlacementNoise noise)
        {
            return new PlacementFilter
            {
                enabled = true,
                range = new Vector2(0.5f, 1.5f),
                smoothness = Vector2.zero,
                noise = noise,
            };
        }

        private static PlacementNoise UniformTextureNoise(byte level)
        {
            return new PlacementNoise
            {
                noiseType = MapNoiseType.None,
                channel = TextureChannel.R,
                amplitude = 1f,
                textureWidth = 2,
                textureHeight = 2,
                texturePixels = new Color32[]
                {
                    new(level, level, level, 255), new(level, level, level, 255),
                    new(level, level, level, 255), new(level, level, level, 255),
                },
            };
        }
    }
}
