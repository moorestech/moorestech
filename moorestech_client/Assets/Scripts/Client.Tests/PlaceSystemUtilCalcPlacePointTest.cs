using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests
{
    // 既存ブロックの各面にカーソルを合わせて隣接設置する際のプレビュー座標計算テスト
    // Tests for preview coordinate calculation when placing adjacent to an existing block
    public class PlaceSystemUtilCalcPlacePointTest
    {
        // 既存ブロックを world (5,5,5)〜(6,6,6) に1x1x1で置いた想定
        // Assume existing 1x1x1 block occupies world (5,5,5)-(6,6,6)

        private static BlockMasterElement MakeUnitBlock()
        {
            return new BlockMasterElement(
                0,
                Guid.Empty,
                "TestBlock",
                "TestBlockType",
                Guid.Empty,
                new Vector3Int(1, 1, 1),
                null,
                null,
                null,
                null,
                true
            );
        }

        [Test]
        public void YX_Origin_北面_に隣接設置_Z方向に重ならないこと()
        {
            // 既存ブロックの-Z面 (z=5) にヒット → 新ブロックは z=4 に置かれるべき
            // Hit on -Z face of existing block → new block origin should be z=4
            var hitPoint = new Vector3(5.5f, 5.5f, 5.0f);

            var pos = PlaceSystemUtil.CalcPlacePoint(MakeUnitBlock(), hitPoint, 0, BlockDirection.North, PreviewSurfaceType.YX_Origin);

            Assert.AreEqual(new Vector3Int(5, 5, 4), pos);
        }

        [Test]
        public void YX_Z_南面_に隣接設置()
        {
            // 既存ブロックの+Z面 (z=6) にヒット → 新ブロックは z=6 に置かれるべき
            // Hit on +Z face → new block origin at z=6
            var hitPoint = new Vector3(5.5f, 5.5f, 6.0f);

            var pos = PlaceSystemUtil.CalcPlacePoint(MakeUnitBlock(), hitPoint, 0, BlockDirection.North, PreviewSurfaceType.YX_Z);

            Assert.AreEqual(new Vector3Int(5, 5, 6), pos);
        }

        [Test]
        public void YZ_Origin_西面_に隣接設置_X方向に重ならないこと()
        {
            // 既存ブロックの-X面 (x=5) にヒット → 新ブロックは x=4 に置かれるべき
            // Hit on -X face → new block origin at x=4
            var hitPoint = new Vector3(5.0f, 5.5f, 5.5f);

            var pos = PlaceSystemUtil.CalcPlacePoint(MakeUnitBlock(), hitPoint, 0, BlockDirection.North, PreviewSurfaceType.YZ_Origin);

            Assert.AreEqual(new Vector3Int(4, 5, 5), pos);
        }

        [Test]
        public void YZ_X_東面_に隣接設置()
        {
            // 既存ブロックの+X面 (x=6) にヒット → 新ブロックは x=6 に置かれるべき
            var hitPoint = new Vector3(6.0f, 5.5f, 5.5f);

            var pos = PlaceSystemUtil.CalcPlacePoint(MakeUnitBlock(), hitPoint, 0, BlockDirection.North, PreviewSurfaceType.YZ_X);

            Assert.AreEqual(new Vector3Int(6, 5, 5), pos);
        }

        [Test]
        public void XZ_Y_上面_に隣接設置()
        {
            // 既存ブロックの+Y面 (y=6) にヒット → 新ブロックは y=6 に置かれるべき
            var hitPoint = new Vector3(5.5f, 6.0f, 5.5f);

            var pos = PlaceSystemUtil.CalcPlacePoint(MakeUnitBlock(), hitPoint, 0, BlockDirection.North, PreviewSurfaceType.XZ_Y);

            Assert.AreEqual(new Vector3Int(5, 6, 5), pos);
        }

        [Test]
        public void XZ_Origin_下面_に隣接設置_Y方向に隙間が空かないこと()
        {
            // 既存ブロックの-Y面 (y=5) にヒット → 新ブロックは y=4 に置かれるべき
            // Hit on -Y face → new block origin at y=4
            var hitPoint = new Vector3(5.5f, 5.0f, 5.5f);

            var pos = PlaceSystemUtil.CalcPlacePoint(MakeUnitBlock(), hitPoint, 0, BlockDirection.North, PreviewSurfaceType.XZ_Origin);

            Assert.AreEqual(new Vector3Int(5, 4, 5), pos);
        }

        [Test]
        public void XZ_Origin_下面_浮動小数点誤差_4_9999_でもギャップが出ないこと()
        {
            // 下から当てたレイは浮動小数点精度により hit.y が 5.0 ではなく 4.9999... を返すことがある
            // → FloorToIntで切り捨てると1ブロック下にズレてしまうバグの再現
            // Ray from below may return hit.y slightly under 5.0 due to floating point imprecision
            // → FloorToInt would drop it one block too low
            var hitPoint = new Vector3(5.5f, 4.9999f, 5.5f);

            var pos = PlaceSystemUtil.CalcPlacePoint(MakeUnitBlock(), hitPoint, 0, BlockDirection.North, PreviewSurfaceType.XZ_Origin);

            Assert.AreEqual(new Vector3Int(5, 4, 5), pos);
        }

        [Test]
        public void YX_Origin_北面_浮動小数点誤差_でもオーバーラップしないこと()
        {
            // -Z側から当てたレイは hit.z が 4.9999... を返すことがある
            var hitPoint = new Vector3(5.5f, 5.5f, 4.9999f);

            var pos = PlaceSystemUtil.CalcPlacePoint(MakeUnitBlock(), hitPoint, 0, BlockDirection.North, PreviewSurfaceType.YX_Origin);

            Assert.AreEqual(new Vector3Int(5, 5, 4), pos);
        }

        [Test]
        public void YZ_Origin_西面_浮動小数点誤差_でもオーバーラップしないこと()
        {
            // -X側から当てたレイは hit.x が 4.9999... を返すことがある
            var hitPoint = new Vector3(4.9999f, 5.5f, 5.5f);

            var pos = PlaceSystemUtil.CalcPlacePoint(MakeUnitBlock(), hitPoint, 0, BlockDirection.North, PreviewSurfaceType.YZ_Origin);

            Assert.AreEqual(new Vector3Int(4, 5, 5), pos);
        }

        // 境界ケース: RoundToIntの±0.5仮定を、面座標を挟んだ両側でチェック
        // Boundary cases: verify the RoundToInt ±0.5 assumption on both sides of the face coordinate

        [Test]
        public void XZ_Origin_下面_hit_5_0001_でも境界を超えないこと()
        {
            // 面座標+ε側(hit=5.0001)でも正しく y=4 に落ちること
            var hitPoint = new Vector3(5.5f, 5.0001f, 5.5f);

            var pos = PlaceSystemUtil.CalcPlacePoint(MakeUnitBlock(), hitPoint, 0, BlockDirection.North, PreviewSurfaceType.XZ_Origin);

            Assert.AreEqual(new Vector3Int(5, 4, 5), pos);
        }

        [Test]
        public void XZ_Y_上面_hit_5_9999_でも境界を超えないこと()
        {
            // 上面 y=6 の面座標-ε側(hit=5.9999)でも y=6 に落ちること
            // Upper face at y=6, hit just below (5.9999) should still resolve to y=6
            var hitPoint = new Vector3(5.5f, 5.9999f, 5.5f);

            var pos = PlaceSystemUtil.CalcPlacePoint(MakeUnitBlock(), hitPoint, 0, BlockDirection.North, PreviewSurfaceType.XZ_Y);

            Assert.AreEqual(new Vector3Int(5, 6, 5), pos);
        }

        [Test]
        public void YX_Z_南面_hit_5_9999_でも境界を超えないこと()
        {
            // 南面 z=6 の -ε側(hit=5.9999) でも z=6 に解決すること
            var hitPoint = new Vector3(5.5f, 5.5f, 5.9999f);

            var pos = PlaceSystemUtil.CalcPlacePoint(MakeUnitBlock(), hitPoint, 0, BlockDirection.North, PreviewSurfaceType.YX_Z);

            Assert.AreEqual(new Vector3Int(5, 5, 6), pos);
        }

        [Test]
        public void YZ_X_東面_hit_5_9999_でも境界を超えないこと()
        {
            // 東面 x=6 の -ε側(hit=5.9999) でも x=6 に解決すること
            var hitPoint = new Vector3(5.9999f, 5.5f, 5.5f);

            var pos = PlaceSystemUtil.CalcPlacePoint(MakeUnitBlock(), hitPoint, 0, BlockDirection.North, PreviewSurfaceType.YZ_X);

            Assert.AreEqual(new Vector3Int(6, 5, 5), pos);
        }

        // surfaceType==null (地面ヒット) のフォールバック経路もテスト
        // Ground-hit fallback path where surfaceType is null
        [Test]
        public void surfaceType_null_地面ヒット時_中央寄せスナップ()
        {
            // 1x1x1ブロック(奇数サイズ)ではheightOffset=0でhit位置をそのまま使う
            // For unit block (odd size), heightOffset=0 uses hit position as-is
            var hitPoint = new Vector3(5.3f, 4.0f, 5.7f);

            var pos = PlaceSystemUtil.CalcPlacePoint(MakeUnitBlock(), hitPoint, 0, BlockDirection.North, (PreviewSurfaceType?)null);

            Assert.AreEqual(new Vector3Int(5, 4, 5), pos);
        }
    }
}
