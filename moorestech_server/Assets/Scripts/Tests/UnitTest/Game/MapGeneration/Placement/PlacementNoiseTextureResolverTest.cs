using System;
using System.IO;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using Game.MapGeneration.Pipeline.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // texturePngPath → 画素への展開と、手書きバイリニアが Unity の GetPixelBilinear と一致することを固定する。
    // 移植元は Texture2D.GetPixelBilinear をそのまま呼んでいたので、一致こそが移植の忠実性そのもの。
    // Pins texturePngPath expansion into pixels and that the hand-written bilinear matches Unity's GetPixelBilinear.
    // The source called Texture2D.GetPixelBilinear directly, so that agreement is the fidelity criterion itself.
    public class PlacementNoiseTextureResolverTest
    {
        private const int Width = 3;
        private const int Height = 2;
        private const string PngRelativePath = "mapGenerate/placementNoise.png";

        private static readonly Color32[] Pixels =
        {
            new(10, 20, 30, 255), new(200, 60, 90, 240), new(40, 255, 10, 200),
            new(90, 10, 180, 160), new(255, 128, 64, 120), new(0, 200, 220, 80),
        };

        private string _serverDataDirectory;

        [SetUp]
        public void SetUp()
        {
            _serverDataDirectory = Path.Combine(Path.GetTempPath(), "placementNoiseTexture-" + Guid.NewGuid());
            var pngPath = Path.Combine(_serverDataDirectory, PngRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(pngPath));
            File.WriteAllBytes(pngPath, ImageConversion.EncodeToPNG(CreateTexture()));
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_serverDataDirectory, true);
        }

        // 4つのノイズ枠すべてが、どのバイオームに置かれていても展開されること。
        // Every one of the four noise slots is expanded, whichever biome holds the prototype.
        [Test]
        public void 全バイオームの全ノイズ枠のPNGを画素へ展開する()
        {
            var config = new TerrainGenerationConfig();
            var grasslandEntry = CreateEntryWithTexturePath();
            var woodsEntry = CreateEntryWithTexturePath();
            config.grassland.treePlacement.prototypes = new[] { grasslandEntry };
            config.woods.treePlacement.prototypes = new[] { woodsEntry };

            PlacementNoiseTextureResolver.Resolve(config, _serverDataDirectory);

            AssertExpanded(grasslandEntry.clusterNoise);
            AssertExpanded(grasslandEntry.clusterNoise2);
            AssertExpanded(grasslandEntry.slopeFilter.noise);
            AssertExpanded(grasslandEntry.curvatureFilter.noise);
            AssertExpanded(woodsEntry.clusterNoise);

            void AssertExpanded(PlacementNoise noise)
            {
                Assert.AreEqual(Width, noise.textureWidth);
                Assert.AreEqual(Height, noise.textureHeight);
                Assert.AreEqual(Pixels, noise.texturePixels);
            }
        }

        [Test]
        public void 空のパスならテクスチャを読み込まない()
        {
            var config = new TerrainGenerationConfig();
            var entry = new TreePrototypeEntry();
            config.grassland.treePlacement.prototypes = new[] { entry };

            PlacementNoiseTextureResolver.Resolve(config, _serverDataDirectory);

            Assert.IsNull(entry.clusterNoise.texturePixels);
            Assert.AreEqual(0, entry.clusterNoise.textureWidth);
        }

        // 欠損はフォールバックせず即例外。マスタ不備を無言で「テクスチャ無し」に化けさせない。
        // A missing file fails loud instead of falling back, so a master gap never degrades into "no texture".
        [Test]
        public void パスが指すPNGが無ければ例外にする()
        {
            var config = new TerrainGenerationConfig();
            var entry = new TreePrototypeEntry();
            entry.clusterNoise.texturePngPath = "mapGenerate/missing.png";
            config.grassland.treePlacement.prototypes = new[] { entry };

            var exception = Assert.Throws<InvalidOperationException>(
                () => PlacementNoiseTextureResolver.Resolve(config, _serverDataDirectory));
            Assert.That(exception.Message, Does.Contain("missing.png"));
        }

        // Unity の GetPixelBilinear はテクセル原点を uv*size に取り、GPU 規約 uv*size-0.5 と半テクセルずれる。
        // 本実装は GPU 規約側を採ったので、その半テクセルだけ UV をずらせば全格子で一致する＝補間核は同一。
        // Unity's GetPixelBilinear puts the texel origin at uv*size, half a texel off the GPU's uv*size-0.5.
        // This implementation follows the GPU rule, so shifting UV by that half texel must match everywhere: same kernel.
        [Test]
        public void 手書きバイリニアはUnityのGetPixelBilinearと半テクセルずれを除いて一致する()
        {
            var config = new TerrainGenerationConfig();
            var entry = CreateEntryWithTexturePath();
            config.grassland.treePlacement.prototypes = new[] { entry };
            PlacementNoiseTextureResolver.Resolve(config, _serverDataDirectory);

            var reference = CreateTexture();
            reference.wrapMode = TextureWrapMode.Clamp;
            reference.filterMode = FilterMode.Bilinear;

            const float TerrainWidth = 800f;
            const float TerrainLength = 400f;
            foreach (var channel in (TextureChannel[])Enum.GetValues(typeof(TextureChannel)))
            {
                var noise = entry.clusterNoise;
                noise.channel = channel;
                noise.amplitude = 1f;

                // 端のクランプ規約まで一致させる意図は無いので、両者が外挿しないテクセル内部だけを回す。
                // The clamp policy at the border is intentionally ours, so only the texel interior is swept.
                for (int i = 0; i <= 12; i++)
                for (int j = 0; j <= 12; j++)
                {
                    float u = i / 12f * (Width - 1) / Width;
                    float v = j / 12f * (Height - 1) / Height;
                    float expected = ChannelOf(reference.GetPixelBilinear(u, v), channel);
                    float actual = Sample(noise, u + 0.5f / Width, v + 0.5f / Height);
                    Assert.AreEqual(expected, actual, 2e-3f, $"channel={channel} u={u} v={v}");
                }
            }

            UnityEngine.Object.DestroyImmediate(reference);

            // UV の端は外挿せずクランプする。左下 UV=(0,0) と右上 UV=(1,1) が隅のテクセルそのものになる。
            // The UV border clamps instead of extrapolating: UV=(0,0) and UV=(1,1) land exactly on the corner texels.
            var red = entry.clusterNoise;
            red.channel = TextureChannel.R;
            red.amplitude = 1f;
            Assert.AreEqual(Pixels[0].r / 255f, Sample(red, 0f, 0f), 1e-4f);
            Assert.AreEqual(Pixels[Pixels.Length - 1].r / 255f, Sample(red, 1f, 1f), 1e-4f);

            #region Internal

            float Sample(PlacementNoise noise, float u, float v) =>
                ManagedNoise.SamplePlacementNoise(noise, u * TerrainWidth, v * TerrainLength, null, 0f, 0f, TerrainWidth, TerrainLength);

            float ChannelOf(Color pixel, TextureChannel channel)
            {
                switch (channel)
                {
                    case TextureChannel.G: return pixel.g;
                    case TextureChannel.B: return pixel.b;
                    case TextureChannel.A: return pixel.a;
                    default: return pixel.r;
                }
            }

            #endregion
        }

        private static Texture2D CreateTexture()
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.SetPixels32(Pixels);
            texture.Apply();
            return texture;
        }

        private static TreePrototypeEntry CreateEntryWithTexturePath()
        {
            var entry = new TreePrototypeEntry();
            entry.clusterNoise.texturePngPath = PngRelativePath;
            entry.clusterNoise2.texturePngPath = PngRelativePath;
            entry.slopeFilter.noise.texturePngPath = PngRelativePath;
            entry.curvatureFilter.noise.texturePngPath = PngRelativePath;
            return entry;
        }
    }
}
