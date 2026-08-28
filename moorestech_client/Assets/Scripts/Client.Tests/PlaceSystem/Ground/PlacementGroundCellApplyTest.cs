using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Game.Block.Interface;
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

        // 地表32.4のセルはY33へ持ち上がる
        // A cell over ground at 32.4 is lifted to Y 33
        [Test]
        public void 端数のある地表は上のセルへ切り上げる()
        {
            CreateGroundSlab(new Vector3(100.5f, 31.9f, 200.5f), new Vector3(6f, 1f, 6f));

            Assert.IsTrue(TryResolve(new Vector3Int(100, 0, 200), 0, out var resolved));
            Assert.AreEqual(new Vector3Int(100, 33, 200), resolved);
        }

        // 整数ちょうどの地表は浮かせない
        // Ground exactly on an integer must not float
        [Test]
        public void 整数ちょうどの地表は浮かせない()
        {
            CreateGroundSlab(new Vector3(300.5f, 31.5f, 400.5f), new Vector3(6f, 1f, 6f));

            Assert.IsTrue(TryResolve(new Vector3Int(300, 0, 400), 0, out var resolved));
            Assert.AreEqual(32, resolved.y);
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

        // 負の高さでも切り上げ規約は変わらない
        // The round-up convention is unchanged for negative heights
        [Test]
        public void 負の高さでも切り上げる()
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
