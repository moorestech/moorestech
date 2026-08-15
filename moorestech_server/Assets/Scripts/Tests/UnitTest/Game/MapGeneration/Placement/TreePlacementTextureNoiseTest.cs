using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Generators.Util;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // 樹木配置側の「ノイズタイプ None でもテクスチャ源があれば有効」というガードを固定する。
    // 移植元 TreePlacementGenerator.cs:335,339,549 の `|| noise.texture != null` に対応する。
    // Pins the tree-placement guard that keeps a noise active with noiseType None as long as a texture
    // source exists, matching `|| noise.texture != null` at the source's TreePlacementGenerator.cs:335,339,549.
    public class TreePlacementTextureNoiseTest
    {
        private const int Resolution = 8;
        private const float TerrainSize = 100f;

        // フィルタノイズの「源なし」は 0（加算の中立値）。テクスチャ源があれば読んだ値を返す。
        // "No source" for a filter noise means 0 (the additive neutral); with a texture it returns what it read.
        [Test]
        public void ノイズタイプNoneでもテクスチャ源があればフィルタノイズを読む()
        {
            var noise = CreateUniformTextureNoise(255);
            noise.noiseType = MapNoiseType.None;

            float withTexture = TreePlacementCommon.SampleFilterNoise(noise, 50f, 50f, null, TerrainSize, TerrainSize);
            Assert.AreEqual(1f, withTexture, 1e-3f);

            var withoutTexture = noise;
            withoutTexture.texturePixels = null;
            Assert.AreEqual(0f, TreePlacementCommon.SampleFilterNoise(withoutTexture, 50f, 50f, null, TerrainSize, TerrainSize));
        }

        // 真っ黒はしきい値以下で棄却、真っ白は通過。ガードを外すとどちらも「クラスタ判定なし」で通ってしまう。
        // Black falls under the threshold and is rejected while white passes; dropping the guard lets both through unjudged.
        [Test]
        public void ノイズタイプNoneでもテクスチャ源があればクラスタ判定が働く()
        {
            Assert.AreEqual(0, PlaceWithClusterTexture(0), "真っ黒なクラスタテクスチャは棄却されるべき");
            Assert.AreEqual(1, PlaceWithClusterTexture(255), "真っ白なクラスタテクスチャは通過するべき");
        }

        private static int PlaceWithClusterTexture(byte level)
        {
            var entry = new TreePrototypeEntry
            {
                mapObjectGuids = new[] { "11111111-0000-0000-0000-000000000001" },
                sharedGridMinDistance = 0f,
                clusterNoiseThreshold = 0.3f,
                clusterNoise = CreateUniformTextureNoise(level),
            };
            entry.clusterNoise.noiseType = MapNoiseType.None;

            // 完全な平地・曲率0にして、クラスタ判定より手前の傾斜フィルタを必ず通す。
            // A perfectly flat, zero-curvature tile so the slope filter ahead of the cluster test always passes.
            var dims = new TerrainDimensions(TerrainSize, TerrainSize, 100f, 0f, 0f, Resolution, 0f, 0f, 1, 0f, 0f);
            var heights = new float[Resolution * Resolution];
            var curvature = new float[Resolution * Resolution];
            var nativeHeights = new NativeArray<float>(heights, Allocator.Temp);
            var placements = new List<PlacementEntry>();

            TreePlacementEntry.TryPlaceEntry(entry, new Vector2(50f, 50f), dims, heights, curvature,
                nativeHeights, null, new TreeDensityConfig(), new SpatialGrid(TerrainSize, TerrainSize, 10f),
                new System.Random(1), placements);

            nativeHeights.Dispose();
            return placements.Count;
        }

        private static PlacementNoise CreateUniformTextureNoise(byte level)
        {
            return new PlacementNoise
            {
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
