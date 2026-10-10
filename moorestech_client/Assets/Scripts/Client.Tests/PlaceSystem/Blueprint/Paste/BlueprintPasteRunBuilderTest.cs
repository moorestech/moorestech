using System.Linq;
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem
{
    public class BlueprintPasteRunBuilderTest
    {
        private readonly List<GameObject> _groundObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var ground in _groundObjects) Object.DestroyImmediate(ground);
            _groundObjects.Clear();
        }

        [TestCase(5, 0, 0, 2, 1, 1, 4, 0, 0)]
        [TestCase(-5, 0, 0, 2, 1, 1, -4, 0, 0)]
        [TestCase(0, 0, 7, 2, 1, 3, 0, 0, 6)]
        [TestCase(0, 0, -7, 2, 1, 3, 0, 0, -6)]
        public void ブロック面では外形寸法の刻みで原点が並ぶTest(int cursorX, int cursorY, int cursorZ, int sizeX, int sizeY, int sizeZ, int lastX, int lastY, int lastZ)
        {
            // 前後の列を外形刻みで地形追従せず生成
            // Build forward and reverse extent steps without terrain following.
            var origins = BlueprintPasteRunBuilder.BuildOrigins(Vector3Int.zero, new Vector3Int(cursorX, cursorY, cursorZ), Vector3Int.zero, new Vector3Int(sizeX, sizeY, sizeZ), PlacementHitSurfaceKind.BlockFace, 0, out var cursorIndex);
            Assert.AreEqual(3, origins.Count);
            Assert.AreEqual(2, cursorIndex);
            Assert.AreEqual(Vector3Int.zero, origins[0].Position);
            Assert.AreEqual(new Vector3Int(lastX, lastY, lastZ), origins[2].Position);
            Assert.IsTrue(origins.All(origin => origin.IsGroundFound));
        }

        [TestCase(PlacementHitSurfaceKind.Ground)]
        [TestCase(PlacementHitSurfaceKind.BlockFace)]
        public void 縦列は地形へ潰さず外形高さずつ積むTest(PlacementHitSurfaceKind surfaceKind)
        {
            // 既に解決したQE込みの始点を保存し縦列にはオフセットを再適用しない
            // Preserve the resolved start including Q/E and do not reapply the offset to vertical runs
            var start = new Vector3Int(7, 32, 9);
            var origins = BlueprintPasteRunBuilder.BuildOrigins(start, new Vector3Int(7, 41, 9), start, new Vector3Int(2, 4, 3), surfaceKind, 2, out var cursorIndex);
            Assert.AreEqual(2, cursorIndex);
            CollectionAssert.AreEqual(new[] { 32, 36, 40 }, origins.Select(origin => origin.Position.y).ToArray());
            Assert.IsTrue(origins.All(origin => origin.Position.x == 7 && origin.Position.z == 9 && origin.IsGroundFound));
        }

        [Test]
        public void 単発の面ヒットも解決済み原点を保つTest()
        {
            var origin = new Vector3Int(5, 34, 9);
            var origins = BlueprintPasteRunBuilder.BuildOrigins(origin, origin, origin, new Vector3Int(3, 4, 2), PlacementHitSurfaceKind.BlockFace, 2, out var cursorIndex);
            Assert.AreEqual(1, origins.Count);
            Assert.AreEqual(0, cursorIndex);
            Assert.AreEqual(origin, origins[0].Position);
            Assert.IsTrue(origins[0].IsGroundFound);
        }

        [TestCase(3, 2)]
        [TestCase(2, 3)]
        public void 地面列のカーソル原点は回転済み底面の最高点とQEを使うTest(int width, int depth)
        {
            var start = new Vector3Int(6000, 0, 7000);
            var footprint = new Vector3Int(width, 4, depth);
            CreateGround(new Vector3(6000 + width * 1.5f, 5f, 7000 + depth * 0.5f), new Vector3(width * 3, 1, depth));

            // 末尾の高地だけ底面探査へ含める
            // Include only the raised tail terrain inside the footprint probe.
            CreateGround(new Vector3(6000 + width * 3 - 0.5f, 14f, 7000 + depth - 0.5f), Vector3.one);
            CreateGround(new Vector3(6000 + width * 3 + 0.5f, 80f, 7000 + depth - 0.5f), Vector3.one);
            Physics.SyncTransforms();

            // カーソルが刻みと列軸から外れても、距離判定には解決済み末尾原点を渡す
            // Even off the stride and run axis, distance checking receives the resolved last origin
            var cursor = start + new Vector3Int(width * 2 + 1, 0, 1);
            var origins = BlueprintPasteRunBuilder.BuildOrigins(start, cursor, start, footprint, PlacementHitSurfaceKind.Ground, 2, out var cursorIndex);
            Assert.AreEqual(2, cursorIndex);
            Assert.IsTrue(origins.All(origin => origin.IsGroundFound));
            Assert.AreEqual(new Vector3Int(6000 + width * 2, 16, 7000), origins[cursorIndex].Position);
            Assert.AreEqual(7, origins[0].Position.y);
        }

        [Test]
        public void 縦列の始点は広い底面の最高点へ補正するTest()
        {
            var anchor = new Vector3Int(6000, 0, 7000);
            var footprint = new Vector3Int(2, 4, 3);
            CreateGround(new Vector3(6001, 5, 7001.5f), new Vector3(2, 1, 3));
            CreateGround(new Vector3(6001.5f, 14, 7002.5f), Vector3.one);
            Physics.SyncTransforms();

            // 低いカーソル段を先に補正してから縦のドラッグ始点へ保存する
            // Adjust the low cursor anchor before preserving it as the vertical drag start
            Assert.IsTrue(BlueprintPasteOriginResolver.TryResolveFootprintOrigin(anchor, footprint, 2,
                PlacementHitSurfaceKind.Ground, out var start));
            var origins = BlueprintPasteRunBuilder.BuildOrigins(anchor, anchor + Vector3Int.up * 8, start, footprint,
                PlacementHitSurfaceKind.Ground, 2, out _);
            CollectionAssert.AreEqual(new[] { 16, 20, 24 }, origins.Select(origin => origin.Position.y).ToArray());
            Assert.IsTrue(origins.All(origin => origin.IsGroundFound));
        }

        [Test]
        public void 横ドラッグで高い底面セルが外れても縦列に変わらないTest()
        {
            var startAnchor = new Vector3Int(6000, 7, 7000);
            var cursorAnchor = new Vector3Int(6004, 7, 7000);
            var footprint = new Vector3Int(2, 4, 3);
            CreateGround(new Vector3(6003, 5, 7001.5f), new Vector3(6, 1, 3));
            CreateGround(new Vector3(6001.5f, 14, 7002.5f), Vector3.one);
            Physics.SyncTransforms();

            // 同じヒット段で始点だけ底面最高点が高い状況を作る
            // Only the starting footprint has a high corner while hit anchors stay level
            Assert.IsTrue(BlueprintPasteOriginResolver.TryResolveFootprintOrigin(startAnchor, footprint, 2,
                PlacementHitSurfaceKind.Ground, out var placementStart));
            Assert.IsTrue(BlueprintPasteOriginResolver.TryResolveFootprintOrigin(cursorAnchor, footprint, 2,
                PlacementHitSurfaceKind.Ground, out var cursorOrigin));
            Assert.AreEqual(16, placementStart.y);
            Assert.AreEqual(7, cursorOrigin.y);
            var origins = BlueprintPasteRunBuilder.BuildOrigins(startAnchor, cursorAnchor, placementStart, footprint,
                PlacementHitSurfaceKind.Ground, 2, out var cursorIndex);

            // X列の各底面へ追従し開始地点の下へコピーを増やさない
            // Follow each horizontal footprint without stacking copies below the start
            Assert.AreEqual(2, cursorIndex);
            CollectionAssert.AreEqual(new[] { 6000, 6002, 6004 }, origins.Select(origin => origin.Position.x).ToArray());
            CollectionAssert.AreEqual(new[] { 16, 7, 7 }, origins.Select(origin => origin.Position.y).ToArray());
            Assert.IsTrue(origins.All(origin => origin.IsGroundFound));
        }

        [Test]
        public void 地形なしは警告を出さず原点の不可状態へ残すTest()
        {
            var position = new Vector3Int(20000, 0, 20000);
            var origins = BlueprintPasteRunBuilder.BuildOrigins(position, position, position, Vector3Int.one, PlacementHitSurfaceKind.Ground, 0, out var cursorIndex);
            Assert.AreEqual(0, cursorIndex);
            Assert.IsFalse(origins[0].IsGroundFound);
            Assert.AreEqual(position, origins[0].Position);
            UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
        }

        private void CreateGround(Vector3 position, Vector3 size)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.layer = LayerConst.GroundLayer;
            ground.transform.position = position;
            ground.transform.localScale = size;
            _groundObjects.Add(ground);
        }
    }
}
