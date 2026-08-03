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

        [TearDown]
        public void TearDown()
        {
            if (_ground != null) Object.DestroyImmediate(_ground);
        }

        [Test]
        public void 四隅の最大高さはZ座標を無視しない()
        {
            // z=0には地表を置かず、対象ブロックの実位置にだけ地表を置く
            // Leave z=0 without ground and place it only under the target block
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.layer = LayerConst.GroundLayer;
            _ground.transform.position = new Vector3(10f, 30f, 10f);
            _ground.transform.localScale = new Vector3(6f, 1f, 6f);
            Physics.SyncTransforms();

            var height = SlopeBlockPlaceSystem.GetBlockFourCornerMaxHeight(
                new Vector3Int(10, 0, 10), BlockDirection.North, Vector3Int.one);

            Assert.AreEqual(30.5f, height, 0.001f, "z=0を探査している");
        }

        [Test]
        public void TryGetGroundPointはXZだけを受け取る()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.layer = LayerConst.GroundLayer;
            _ground.transform.position = new Vector3(4f, 12f, 8f);
            _ground.transform.localScale = new Vector3(4f, 1f, 4f);
            Physics.SyncTransforms();

            Assert.IsTrue(SlopeBlockPlaceSystem.TryGetGroundPoint(4f, 8f, out var groundPoint));
            Assert.AreEqual(12.5f, groundPoint.y, 0.001f);
            Assert.IsFalse(SlopeBlockPlaceSystem.TryGetGroundPoint(4f, 0f, out _), "地表の無いZでヒットしている");
        }
    }
}
