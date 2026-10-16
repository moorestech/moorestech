using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Ground
{
    // 地表探査が占有セルのXZ平面だけを走査することを検証する
    // Verify that ground probing scans only the occupied cells' XZ plane
    public class GroundHeightProbeTest
    {
        private readonly List<GameObject> _slabs = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var slab in _slabs) Object.DestroyImmediate(slab);
            _slabs.Clear();
        }

        [Test]
        public void 占有セルの最大高さはZ座標を無視しない()
        {
            // 探査点を非対称なXZに置き、XとZを入れ替えたら地表の無い場所を探査するようにする
            // Put the probe points on asymmetric XZ so that swapping X and Z probes where no ground exists
            var blockPos = new Vector3Int(10, 0, 20);

            // セル全体を覆う低い段
            // The low slab covering the whole cell
            CreateGroundSlab(new Vector3(10.5f, 30f, 20.5f), new Vector3(6f, 1f, 6f));

            // セル内の(10.9,20.9)付近だけが乗る高い段。最大を取らなければこの高さは返らない
            // The high slab under (10.9, 20.9) inside the cell; only taking the max returns this height
            CreateGroundSlab(new Vector3(10.9f, 34f, 20.9f), new Vector3(0.3f, 1f, 0.3f));

            Assert.IsTrue(GroundHeightProbe.TryGetFootprintMaxGroundHeight(
                blockPos, BlockDirection.North, Vector3Int.one, out var height));

            Assert.AreEqual(34.5f, height, 0.01f, "占有セルの最大を取れていない");
        }

        // 占有していない隣接セルの地形はYを持ち上げない
        // The terrain of a neighbouring, unoccupied cell never lifts Y
        [Test]
        public void 隣接セルの地形は拾わない()
        {
            CreateGroundSlab(new Vector3(101f, 9.9f, 200.5f), new Vector3(6f, 1f, 6f));

            // 隣のセル(101)の内側だけを占める高い柱。セル境界ちょうどを探査すると拾ってしまう
            // A tall pillar inside the neighbouring cell 101 only; probing exactly on the border would pick it up
            CreateGroundSlab(new Vector3(101.25f, 20f, 200.5f), new Vector3(0.5f, 1f, 6f));

            Assert.IsTrue(GroundHeightProbe.TryGetFootprintMaxGroundHeight(
                new Vector3Int(100, 0, 200), BlockDirection.North, Vector3Int.one, out var height));

            Assert.AreEqual(10.4f, height, 0.01f, "隣接セルの柱を拾っている");
        }

        // 複数セルを占めるブロックは占有セルすべての地形を見る
        // A multi-cell block looks at the terrain of every cell it occupies
        [Test]
        public void 複数セルのブロックは全占有セルを見る()
        {
            CreateGroundSlab(new Vector3(301f, 4.9f, 401f), new Vector3(6f, 1f, 6f));

            // 占有範囲の奥のセル(301,401)だけを持ち上げる
            // Lifts only the far cell (301, 401) of the footprint
            CreateGroundSlab(new Vector3(301.5f, 11.9f, 401.5f), Vector3.one);

            Assert.IsTrue(GroundHeightProbe.TryGetFootprintMaxGroundHeight(
                new Vector3Int(300, 0, 400), BlockDirection.North, new Vector3Int(2, 1, 2), out var height));

            Assert.AreEqual(12.4f, height, 0.01f);
        }

        [Test]
        public void 地表が無ければ失敗する()
        {
            Assert.IsFalse(GroundHeightProbe.TryGetFootprintMaxGroundHeight(
                new Vector3Int(5000, 0, 5000), BlockDirection.North, Vector3Int.one, out _));
        }

        [Test]
        public void TryGetGroundPointはXZだけを受け取る()
        {
            CreateGroundSlab(new Vector3(4f, 12f, 8f), new Vector3(4f, 1f, 4f));

            Assert.IsTrue(GroundHeightProbe.TryGetGroundPoint(4f, 8f, out var groundPoint));
            Assert.AreEqual(12.5f, groundPoint.y, 0.01f);
            Assert.IsFalse(GroundHeightProbe.TryGetGroundPoint(4f, 0f, out _), "地表の無いZでヒットしている");
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
