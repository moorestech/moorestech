using Client.Common;
using Client.Game.InGame.BlockSystem;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem
{
    // 地表探査がXZ平面を正しく走査することを検証する
    // Verify that ground probing scans the XZ plane correctly
    public class SlopeBlockGroundProbeTest
    {
        private GameObject _ground;
        private GameObject _highStep;

        [TearDown]
        public void TearDown()
        {
            if (_ground != null) Object.DestroyImmediate(_ground);
            if (_highStep != null) Object.DestroyImmediate(_highStep);
        }

        [Test]
        public void 四隅の最大高さはZ座標を無視しない()
        {
            // 四隅を非対称なXZに置き、XとZを入れ替えたら地表の無い場所を探査するようにする
            // Put the corners on asymmetric XZ so that swapping X and Z probes where no ground exists
            var blockPos = new Vector3Int(10, 0, 20);

            // 四隅を覆う低い段
            // The low slab covering all four corners
            _ground = CreateGroundSlab(new Vector3(10.5f, 30f, 20.5f), new Vector3(6f, 1f, 6f));

            // (11,21)の1点だけが乗る高い段。最大を取らなければこの高さは返らない
            // The high slab under the single corner (11,21); only taking the max returns this height
            _highStep = CreateGroundSlab(new Vector3(11f, 34f, 21f), Vector3.one);

            Assert.IsTrue(SlopeBlockPlaceSystem.TryGetBlockFourCornerMaxHeight(
                blockPos, BlockDirection.North, Vector3Int.one, out var height));

            Assert.AreEqual(34.5f, height, 0.001f, "四隅の最大を取れていない");
        }

        [Test]
        public void TryGetGroundPointはXZだけを受け取る()
        {
            _ground = CreateGroundSlab(new Vector3(4f, 12f, 8f), new Vector3(4f, 1f, 4f));

            Assert.IsTrue(SlopeBlockPlaceSystem.TryGetGroundPoint(4f, 8f, out var groundPoint));
            Assert.AreEqual(12.5f, groundPoint.y, 0.001f);
            Assert.IsFalse(SlopeBlockPlaceSystem.TryGetGroundPoint(4f, 0f, out _), "地表の無いZでヒットしている");
        }

        private static GameObject CreateGroundSlab(Vector3 position, Vector3 scale)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.layer = LayerConst.GroundLayer;
            slab.transform.position = position;
            slab.transform.localScale = scale;
            Physics.SyncTransforms();
            return slab;
        }
    }
}
