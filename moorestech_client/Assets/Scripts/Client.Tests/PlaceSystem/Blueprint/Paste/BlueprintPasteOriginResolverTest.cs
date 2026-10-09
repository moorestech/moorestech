using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem
{
    public class BlueprintPasteOriginResolverTest
    {
        [TestCase(PreviewSurfaceType.YZ_X, 5, 9)]
        [TestCase(PreviewSurfaceType.YZ_Origin, 2, 9)]
        [TestCase(PreviewSurfaceType.YX_Z, 4, 10)]
        [TestCase(PreviewSurfaceType.YX_Origin, 4, 8)]
        public void 側面は面に接し最下段をカーソル段へそろえるTest(PreviewSurfaceType surface, int expectedX, int expectedZ)
        {
            // 全側面で垂直軸の接し方と平行軸の中心寄せを確かめる
            // Verify normal-axis contact and parallel-axis centering on all side faces
            var origin = BlueprintPasteOriginResolver.ResolveOrigin(new Vector3Int(3, 4, 2), new Vector3(5f, 32.4f, 10.3f), surface, 0f, 0);
            Assert.AreEqual(new Vector3Int(expectedX, 32, expectedZ), origin);
        }

        [TestCase(2, 34)]
        [TestCase(-2, 30)]
        public void 側面のQEは縦へ足すTest(int offset, int expectedY)
        {
            var origin = BlueprintPasteOriginResolver.ResolveOrigin(new Vector3Int(3, 4, 2), new Vector3(5f, 32.4f, 10.3f), PreviewSurfaceType.YZ_X, 0f, offset);
            Assert.AreEqual(expectedY, origin.y);
        }

        [Test]
        public void 負の高さの側面も床関数で最下段をそろえるTest()
        {
            var origin = BlueprintPasteOriginResolver.ResolveOrigin(new Vector3Int(3, 4, 2), new Vector3(5f, -0.2f, 10.3f), PreviewSurfaceType.YZ_X, 0f, 0);
            Assert.AreEqual(-1, origin.y);
        }

        [Test]
        public void 地面はXZがカーソル中心Test()
        {
            var origin = BlueprintPasteOriginResolver.ResolveOrigin(new Vector3Int(4, 2, 3), new Vector3(10.2f, 32f, 10.7f), null, 0f, 0);
            Assert.AreEqual(new Vector3Int(8, 32, 9), origin);
        }

        [Test]
        public void 地面のQEは縦へ一度だけ足すTest()
        {
            var origin = BlueprintPasteOriginResolver.ResolveOrigin(new Vector3Int(4, 2, 3), new Vector3(10.2f, 32f, 10.7f), null, 0f, 2);
            Assert.AreEqual(new Vector3Int(8, 34, 9), origin);
        }

        [TestCase(PreviewSurfaceType.XZ_Y, 35)]
        [TestCase(PreviewSurfaceType.XZ_Origin, 32)]
        public void 上下面は通常の面外側へ置きQEを足すTest(PreviewSurfaceType surface, int expectedY)
        {
            var origin = BlueprintPasteOriginResolver.ResolveOrigin(new Vector3Int(2, 3, 2), new Vector3(4.2f, 33f, 4.2f), surface, 0f, 2);
            Assert.AreEqual(new Vector3Int(3, expectedY, 3), origin);
        }
    }
}
