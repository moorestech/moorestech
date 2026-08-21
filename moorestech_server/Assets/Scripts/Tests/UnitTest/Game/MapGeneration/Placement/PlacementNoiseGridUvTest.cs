using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // テクスチャノイズの UV 正規化基準が「タイル1枚」ではなく「格子全体」であることを固定する。
    // タイル幅で割ると2枚目以降の worldX が UV=1 を超え、GetPixelBilinear が常時 Clamp に落ちて
    // 残りのタイルが最右列テクセルの定数で塗られる（実データは texturePngPath が空なので気づけない）。
    // Pins that the texture noise normalizes UV over the whole grid, not one tile.
    // Dividing by the tile width pushes every tile past UV=1, pinning GetPixelBilinear to its clamp so the
    // remaining tiles are painted with the rightmost texel's constant (invisible in shipped data: every texturePngPath is empty).
    public class PlacementNoiseGridUvTest
    {
        private const float TileWidth = 1000f;
        private const float TileLength = 1000f;

        // 2x2 タイルの格子。1タイルだけの世界では規約差が現れないので2枚以上が要る。
        // A 2x2 grid; a single-tile world cannot show the difference between the two rules.
        private const float GridWidth = TileWidth * 2f;
        private const float GridLength = TileLength * 2f;

        // R が横方向に 0→1/3→2/3→1 と並ぶ 4x4。テクセル中心の外側でしか区別できない点を選べるだけの幅がある。
        // A 4x4 whose R runs 0, 1/3, 2/3, 1 across; wide enough to pick points that differ only outside the texel centers.
        private const int TextureSize = 4;

        // 格子中央（タイル境界）を挟む2点。ワールドで 2m しか離れていない。
        // Two points straddling the grid's middle, the tile boundary; only 2m apart in world space.
        private const float LeftOfSeam = 999f;
        private const float RightOfSeam = 1001f;

        // 本番の格子原点はスポーン探索の worldOffset 由来で、0 から大きく離れた負値にもなる。
        // In production the grid origin comes from the spawn search's worldOffset and can sit far from zero, negative included.
        private const float ShiftedGridOrigin = -12345.6f;

        [Test]
        public void タイル境界を挟んでテクスチャ値が連続する()
        {
            var left = Sample(LeftOfSeam);
            var right = Sample(RightOfSeam);

            // タイル幅基準なら左は UV≈1 の Clamp で 1.0、右は UV≈0 で 0.0 になり 1.0 の段差が立つ。
            // On the tile-width rule the left lands on the UV≈1 clamp at 1.0 and the right at UV≈0 giving 0.0, a full step of 1.0.
            Assert.AreEqual(left, right, 0.01f, $"タイル境界でテクスチャ値が {left} から {right} へ跳んでいる");
            Assert.AreEqual(0.4993f, left, 1e-3f);
            Assert.AreEqual(0.5007f, right, 1e-3f);
        }

        // 格子原点の減算が落ちると UV が [0,1] を大きく外れ、格子全体が端テクセルの定数で塗られる。
        // Dropping the grid-origin subtraction throws UV far outside [0,1] and paints the whole grid with the edge texel's constant.
        [Test]
        public void 格子原点がずれても同じ格子内相対位置なら同じテクセルを引く()
        {
            // 原点 0 の格子の 1500m と、原点 -12345.6 の格子の同じ相対位置。どちらも格子内 75% の点。
            // 1500m on the zero-origin grid and the same relative spot on the -12345.6 origin grid; both sit 75% into the grid.
            var atZeroOrigin = SampleFromOrigin(1500f, 0f);
            var atShiftedOrigin = SampleFromOrigin(ShiftedGridOrigin + 1500f, ShiftedGridOrigin);

            Assert.AreEqual(atZeroOrigin, atShiftedOrigin, 1e-4f,
                $"格子原点をずらすと値が {atZeroOrigin} から {atShiftedOrigin} へ動く＝原点の減算が効いていない");

            // 対照: 減算が落ちれば UV は負の大きな値になり端テクセルへ Clamp される。0.833 と 0 は別物と見分けられる。
            // Control: without the subtraction the UV goes far negative and clamps to the edge texel; 0.833 and 0 are distinguishable.
            Assert.That(atZeroOrigin, Is.Not.EqualTo(0f).Within(1e-3f), "フィクスチャが端テクセルの値と区別できていない");
        }

        [Test]
        public void 隣のタイル内でもテクスチャの階調が残る()
        {
            // タイル幅基準では 1500 も 1900 も UV>1 で Clamp され、2枚目のタイル全体が同じ定数で塗られる。
            // On the tile-width rule both 1500 and 1900 exceed UV=1 and clamp, painting the whole second tile one constant.
            Assert.AreEqual(2f / 3f + 1f / 6f, Sample(1500f), 1e-3f);
            Assert.AreEqual(1f, Sample(1900f), 1e-3f);
        }

        // 縦は格子中央に固定して横方向の階調だけを見る。格子原点は呼び出し側が選ぶ。
        // The vertical stays at the grid's middle so only the horizontal ramp is read; the caller picks the grid origin.
        private static float Sample(float worldX)
        {
            return SampleFromOrigin(worldX, 0f);
        }

        private static float SampleFromOrigin(float worldX, float gridOriginX)
        {
            return ManagedNoise.SamplePlacementNoise(
                CreateHorizontalRamp(), worldX, gridOriginX + GridLength * 0.5f, null,
                gridOriginX: gridOriginX, gridOriginZ: gridOriginX, gridWidth: GridWidth, gridLength: GridLength);
        }

        private static PlacementNoise CreateHorizontalRamp()
        {
            var pixels = new Color32[TextureSize * TextureSize];
            for (var y = 0; y < TextureSize; y++)
                for (var x = 0; x < TextureSize; x++)
                    pixels[y * TextureSize + x] = new Color32((byte)(x * 85), 0, 0, 255);

            return new PlacementNoise
            {
                channel = TextureChannel.R,
                texturePixels = pixels,
                textureWidth = TextureSize,
                textureHeight = TextureSize,
                amplitude = 1f,
            };
        }
    }
}
