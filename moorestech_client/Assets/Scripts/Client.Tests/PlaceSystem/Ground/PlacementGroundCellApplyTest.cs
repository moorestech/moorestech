using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

namespace Client.Tests.PlaceSystem.Ground
{
    // 実コライダーを置いてセル位置の地形解決を検証する
    // Verify the terrain resolution of a cell position against real colliders
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
        public void 端数のある地表の上のセルへ持ち上がる()
        {
            CreateGroundSlab(new Vector3(100.5f, 31.9f, 200.5f), new Vector3(6f, 1f, 6f));

            var resolved = PlacementGroundCellResolver.ResolveCellFromGround(
                new Vector3Int(100, 0, 200), BlockDirection.North, Vector3Int.one, 0);

            Assert.AreEqual(new Vector3Int(100, 33, 200), resolved);
        }

        // 四隅のうち最も高い地表に合わせる
        // The highest of the four corners wins
        [Test]
        public void 四隅の最高点に合わせる()
        {
            CreateGroundSlab(new Vector3(300.5f, 9.5f, 400.5f), new Vector3(6f, 1f, 6f));
            CreateGroundSlab(new Vector3(301f, 14.2f, 401f), Vector3.one);

            var resolved = PlacementGroundCellResolver.ResolveCellFromGround(
                new Vector3Int(300, 0, 400), BlockDirection.North, Vector3Int.one, 0);

            // 高い段の上面は 14.2 + 0.5 = 14.7 なのでセルYは15
            // The high slab's top is 14.2 + 0.5 = 14.7, so the cell Y is 15
            Assert.AreEqual(15, resolved.y);
        }

        // 手動オフセットは地形解決後に加算される
        // The manual offset is added after the terrain resolution
        [Test]
        public void 手動オフセットが加算される()
        {
            CreateGroundSlab(new Vector3(500.5f, 19.9f, 600.5f), new Vector3(6f, 1f, 6f));

            var resolved = PlacementGroundCellResolver.ResolveCellFromGround(
                new Vector3Int(500, 0, 600), BlockDirection.North, Vector3Int.one, 3);

            Assert.AreEqual(24, resolved.y);
        }

        // 地表が無いセルは元のY（=呼び出し側の値）を保つ
        // A cell with no ground keeps its original Y
        [Test]
        public void 地表が無いセルは元のYを保つ()
        {
            var original = new Vector3Int(900, 7, 900);

            var resolved = PlacementGroundCellResolver.ResolveCellFromGround(
                original, BlockDirection.North, Vector3Int.one, 0);

            Assert.AreEqual(original, resolved);
        }

        // XZは書き換えない
        // XZ is never rewritten
        [Test]
        public void XZは書き換えない()
        {
            CreateGroundSlab(new Vector3(700.5f, 4.9f, 800.5f), new Vector3(6f, 1f, 6f));

            var resolved = PlacementGroundCellResolver.ResolveCellFromGround(
                new Vector3Int(700, 0, 800), BlockDirection.North, Vector3Int.one, 0);

            Assert.AreEqual(700, resolved.x);
            Assert.AreEqual(800, resolved.z);
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
