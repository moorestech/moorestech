using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Game.Block.Interface;
using Game.MapGeneration.Surface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Ground
{
    // 実コライダーを置いてセル位置の地形解決と丸めを検証する
    // Verify the terrain resolution and rounding of a cell position against real colliders
    public class PlacementGroundCellApplyTest
    {
        private readonly List<GameObject> _slabs = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var slab in _slabs) Object.DestroyImmediate(slab);
            _slabs.Clear();
        }

        // 地表32.4はY32だと沈みすぎ、Y33へ
        // Ground 32.4 sinks Y 32 too much, so Y 33
        [Test]
        public void 沈みが3分の1mを超える地表は1段上げる()
        {
            CreateGroundSlab(new Vector3(100.5f, 31.9f, 200.5f), new Vector3(6f, 1f, 6f));

            Assert.IsTrue(TryResolve(new Vector3Int(100, 0, 200), 0, out var resolved));
            Assert.AreEqual(new Vector3Int(100, 33, 200), resolved);
        }

        // 地表32.2は0.2m沈みなので据置
        // Ground 32.2 sinks only 0.2 m, so it stays
        [Test]
        public void 沈みが3分の1m以内の地表はそのセルに置く()
        {
            CreateGroundSlab(new Vector3(110.5f, 31.7f, 210.5f), new Vector3(6f, 1f, 6f));

            Assert.IsTrue(TryResolve(new Vector3Int(110, 0, 210), 0, out var resolved));
            Assert.AreEqual(32, resolved.y);
        }

        // 整数ちょうどの地表は沈めない
        // Ground exactly on an integer must not sink
        [Test]
        public void 整数ちょうどの地表は沈めない()
        {
            CreateGroundSlab(new Vector3(300.5f, 31.5f, 400.5f), new Vector3(6f, 1f, 6f));

            Assert.IsTrue(TryResolve(new Vector3Int(300, 0, 400), 0, out var resolved));
            Assert.AreEqual(32, resolved.y);
        }

        // 許容量1/3m＋余裕の境界を固定
        // Pin the 1/3 m allowance plus margin boundary
        [TestCase(32f, 32)]
        [TestCase(32.2f, 32)]
        [TestCase(32.333f, 32)]
        [TestCase(32.334f, 32)]
        [TestCase(32.336f, 33)]
        [TestCase(32.4f, 33)]
        [TestCase(31.99f, 32)]
        [TestCase(32f - 1e-4f, 32)]
        public void 沈みが3分の1m以内に収まる最も低いセルを返す(float groundMax, int expectedCellY)
        {
            Assert.AreEqual(expectedCellY, PlacementGroundCellResolver.ResolveCellY(groundMax, 0));
        }

        // 鉱脈パッド相当の地表は整数セルになる
        // Vein-pad-like ground lands on the integer cell
        [Test]
        public void 格子数段ぶん整数を下回る地表は整数セルへ収める()
        {
            var step = 600f / TerrainHeightStorage.Steps;
            var padUndershoot = step + (float)TerrainHeightStorage.MiningBottomClearanceMeters;

            Assert.AreEqual(10, PlacementGroundCellResolver.ResolveCellY(10f - 0.9f * step, 0));
            Assert.AreEqual(10, PlacementGroundCellResolver.ResolveCellY(10f - padUndershoot - 0.0015f, 0));
            Assert.AreEqual(10, PlacementGroundCellResolver.ResolveCellY(10f - 1.5f * step, 0));
        }

        // 手動オフセットは地形解決後に加算される
        // The manual offset is added after the terrain resolution
        [Test]
        public void 手動オフセットが加算される()
        {
            CreateGroundSlab(new Vector3(500.5f, 19.9f, 600.5f), new Vector3(6f, 1f, 6f));

            Assert.IsTrue(TryResolve(new Vector3Int(500, 0, 600), 3, out var resolvedUp));
            Assert.AreEqual(24, resolvedUp.y);

            Assert.IsTrue(TryResolve(new Vector3Int(500, 0, 600), -2, out var resolvedDown));
            Assert.AreEqual(19, resolvedDown.y);
        }

        // 負の高さも同規則（地表-3.4はY-3）
        // Negative heights follow the same rule (ground -3.4 gives Y -3)
        [Test]
        public void 負の高さでも同じ沈み許容量で決める()
        {
            CreateGroundSlab(new Vector3(700.5f, -3.9f, 800.5f), new Vector3(6f, 1f, 6f));

            Assert.IsTrue(TryResolve(new Vector3Int(700, 0, 800), 0, out var resolved));
            Assert.AreEqual(-3, resolved.y);
        }

        // XZは書き換えない
        // XZ is never rewritten
        [Test]
        public void XZは書き換えない()
        {
            CreateGroundSlab(new Vector3(900.5f, 4.9f, 1000.5f), new Vector3(6f, 1f, 6f));

            Assert.IsTrue(TryResolve(new Vector3Int(900, 0, 1000), 0, out var resolved));
            Assert.AreEqual(900, resolved.x);
            Assert.AreEqual(1000, resolved.z);
        }

        // 地表が無いセルは失敗を返し、呼び出し側が設置不可として扱えるようにする
        // A cell with no ground fails so the caller can block it
        [Test]
        public void 地表が無いセルは失敗を返す()
        {
            Assert.IsFalse(TryResolve(new Vector3Int(4000, 7, 4000), 0, out _));
        }

        private static bool TryResolve(Vector3Int cellPosition, int heightOffset, out Vector3Int resolvedPosition)
        {
            return PlacementGroundCellResolver.TryResolveCellFromGround(
                cellPosition, BlockDirection.North, Vector3Int.one, heightOffset, out resolvedPosition);
        }

        private void CreateGroundSlab(Vector3 position, Vector3 scale)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.layer = LayerConst.GroundLayer;
            slab.transform.position = position;
            slab.transform.localScale = scale;
            Physics.SyncTransforms();
            _slabs.Add(slab);
        }
    }
}
