using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // PlacementNoise のテクスチャ源のサンプリング規約を固定する。
    // ①4近傍のバイリニア補間であること ②channel の指す成分を読むこと ③U=worldX/幅・V=worldZ/長さであること。
    // Pins the sampling contract of the PlacementNoise texture source: bilinear over 4 neighbours,
    // the component named by channel, and U=worldX/width / V=worldZ/length.
    public class PlacementNoiseTextureTest
    {
        [Test]
        public void テクスチャノイズはチャンネル指定のバイリニア補間値を返す()
        {
            // 2x2のRGBAピクセル(左下R=0, 右下R=1, 左上R=0, 右上R=1)で中央をサンプルするとR=0.5
            // Sampling the center of a 2x2 texture (R: 0,1,0,1) bilinearly yields R=0.5
            var noise = new PlacementNoise
            {
                channel = TextureChannel.R,
                texturePixels = new Color32[] { new(0, 0, 0, 255), new(255, 0, 0, 255), new(0, 0, 0, 255), new(255, 0, 0, 255) },
                textureWidth = 2,
                textureHeight = 2,
                amplitude = 1f,
            };
            float value = ManagedNoise.SamplePlacementNoise(noise, worldX: 500f, worldZ: 500f, offsets: null,
                gridOriginX: 0f, gridOriginZ: 0f, gridWidth: 1000f, gridLength: 1000f);
            Assert.AreEqual(0.5f, value, 1e-2f);
        }

        [Test]
        public void テクスチャ未設定かつノイズNoneなら1を返す()
        {
            var noise = new PlacementNoise { noiseType = MapNoiseType.None };
            Assert.AreEqual(1f, ManagedNoise.SamplePlacementNoise(noise, 0f, 0f, null, 0f, 0f, 0f, 0f));
        }

        // 4成分が別々の値を持つテクスチャの同一点を読み、channel ごとに違う数が返ることを見る。
        // Reads one point of a texture whose 4 components differ, so each channel must yield a different number.
        [Test]
        public void チャンネル指定ごとに異なる成分を読む()
        {
            Assert.AreEqual(0.5f, SampleCenter(TextureChannel.R), 1e-3f);
            Assert.AreEqual(64f / 255f, SampleCenter(TextureChannel.G), 1e-3f);
            Assert.AreEqual(96f / 255f, SampleCenter(TextureChannel.B), 1e-3f);
            Assert.AreEqual(50f / 255f, SampleCenter(TextureChannel.A), 1e-3f);

            float SampleCenter(TextureChannel channel)
            {
                var noise = CreateFixture(channel);
                return ManagedNoise.SamplePlacementNoise(noise, 500f, 500f, null, 0f, 0f, 1000f, 1000f);
            }
        }

        // R は x のみ、G は y のみに依存するフィクスチャを非正方の地形で読む。
        // 上下反転・軸入れ替え・幅と長さの取り違えのいずれが起きても値がずれる点を選んである。
        // Reads a fixture where R varies only along x and G only along y, on a non-square terrain.
        // The sample point is chosen so a vertical flip, an axis swap, or a width/length mixup all shift the value.
        [Test]
        public void UVは横がworldXで縦がworldZに対応する()
        {
            // px=0.75, py=0.25 に落ちる点。R は x 方向に 0→1 なので 0.75、G は y 方向に 0→128/255 なので その1/4。
            // The point lands on px=0.75, py=0.25: R runs 0->1 along x giving 0.75, G runs 0->128/255 along y giving a quarter of it.
            Assert.AreEqual(0.75f, Sample(TextureChannel.R), 1e-3f);
            Assert.AreEqual(32f / 255f, Sample(TextureChannel.G), 1e-3f);

            float Sample(TextureChannel channel)
            {
                var noise = CreateFixture(channel);
                return ManagedNoise.SamplePlacementNoise(noise, 625f, 187.5f, null, 0f, 0f, 1000f, 500f);
            }
        }

        // テクスチャ源はノイズタイプより優先される（移植元 ManagedNoise.cs:141-152 の分岐順）。
        // The texture source wins over the noise type (branch order of the source's ManagedNoise.cs:141-152).
        [Test]
        public void テクスチャがあればノイズタイプより優先される()
        {
            // Mathf.PerlinNoise は座標が大きいと 0.5 に潰れるので、小さいオフセットと低周波で意味のある値を出す。
            // Mathf.PerlinNoise collapses to 0.5 at large coordinates, so small offsets and a low frequency keep it meaningful.
            var offsets = new[] { new Vector2(0.13f, 0.71f), new Vector2(1.7f, 2.3f), new Vector2(3.1f, 0.5f), new Vector2(4.2f, 5.9f) };
            var noise = CreateFixture(TextureChannel.R);
            noise.noiseType = MapNoiseType.FBM;
            noise.frequency = 0.01f;

            // フィクスチャがそもそも2経路を区別できることを先に確かめる（偶然の一致で無力化させない）。
            // First confirm the fixture can tell the two paths apart, so a coincidental match cannot neuter the test.
            var withoutTexture = noise;
            withoutTexture.texturePixels = null;
            float noiseValue = ManagedNoise.SamplePlacementNoise(withoutTexture, 625f, 187.5f, offsets, 0f, 0f, 1000f, 500f);
            Assert.That(noiseValue, Is.Not.EqualTo(0.75f).Within(1e-2f));

            float value = ManagedNoise.SamplePlacementNoise(noise, 625f, 187.5f, offsets, 0f, 0f, 1000f, 500f);
            Assert.AreEqual(0.75f, value, 1e-3f);
        }

        // 戻り値の式 (value + offset + balance) * amplitude をテクスチャ経路でも固定する。
        // Pins the return expression (value + offset + balance) * amplitude on the texture path too.
        [Test]
        public void offsetとbalanceを足してamplitudeを掛けた値を返す()
        {
            var noise = CreateFixture(TextureChannel.R);
            noise.offset = 0.25f;
            noise.balance = 0.1f;
            noise.amplitude = 2f;

            float value = ManagedNoise.SamplePlacementNoise(noise, 500f, 500f, null, 0f, 0f, 1000f, 1000f);
            Assert.AreEqual((0.5f + 0.25f + 0.1f) * 2f, value, 1e-3f);
        }

        // R は x のみ・G は y のみに依存し、B/A は中央値が R/G と重ならないように選んだ 2x2。
        // A 2x2 where R depends only on x and G only on y, with B/A picked so their centers differ from R/G.
        public static PlacementNoise CreateFixture(TextureChannel channel)
        {
            return new PlacementNoise
            {
                channel = channel,
                texturePixels = new Color32[]
                {
                    new(0, 0, 0, 0),       // x=0, y=0
                    new(255, 0, 64, 0),    // x=1, y=0
                    new(0, 128, 128, 0),   // x=0, y=1
                    new(255, 128, 192, 200), // x=1, y=1
                },
                textureWidth = 2,
                textureHeight = 2,
                amplitude = 1f,
            };
        }
    }
}
