using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem
{
    public class BlueprintPasteRunBuilderTest
    {
        [TestCase(5, 0, 0, 2, 1, 1, 4, 0, 0)]
        [TestCase(-5, 0, 0, 2, 1, 1, -4, 0, 0)]
        [TestCase(0, 0, 7, 2, 1, 3, 0, 0, 6)]
        [TestCase(0, 0, -7, 2, 1, 3, 0, 0, -6)]
        public void ブロック面では外形寸法の刻みで原点が並ぶ(int cursorX, int cursorY, int cursorZ, int sizeX, int sizeY, int sizeZ, int lastX, int lastY, int lastZ)
        {
            // 前進と後退の列は始点から外形刻みで並び地形を引かない
            // Forward and reverse runs keep footprint strides from the start without probing terrain
            var origins = BlueprintPasteRunBuilder.BuildOrigins(Vector3Int.zero, new Vector3Int(cursorX, cursorY, cursorZ), new Vector3Int(sizeX, sizeY, sizeZ), PlacementHitSurfaceKind.BlockFace, 0);
            Assert.AreEqual(3, origins.Count);
            Assert.AreEqual(Vector3Int.zero, origins[0].Position);
            Assert.AreEqual(new Vector3Int(lastX, lastY, lastZ), origins[2].Position);
            Assert.IsTrue(origins.All(origin => origin.IsGroundFound));
        }

        [TestCase(PlacementHitSurfaceKind.Ground)]
        [TestCase(PlacementHitSurfaceKind.BlockFace)]
        public void 縦列は地形へ潰さず外形高さずつ積む(PlacementHitSurfaceKind surfaceKind)
        {
            // 既に解決したQE込みの始点を保存し縦列にはオフセットを再適用しない
            // Preserve the resolved start including Q/E and do not reapply the offset to vertical runs
            var start = new Vector3Int(7, 32, 9);
            var origins = BlueprintPasteRunBuilder.BuildOrigins(start, new Vector3Int(7, 41, 9), new Vector3Int(2, 4, 3), surfaceKind, 2);
            CollectionAssert.AreEqual(new[] { 32, 36, 40 }, origins.Select(origin => origin.Position.y).ToArray());
            Assert.IsTrue(origins.All(origin => origin.Position.x == 7 && origin.Position.z == 9 && origin.IsGroundFound));
        }

        [Test]
        public void 単発の面ヒットも解決済み原点を保つ()
        {
            var origin = new Vector3Int(5, 34, 9);
            var origins = BlueprintPasteRunBuilder.BuildOrigins(origin, origin, new Vector3Int(3, 4, 2), PlacementHitSurfaceKind.BlockFace, 2);
            Assert.AreEqual(1, origins.Count);
            Assert.AreEqual(origin, origins[0].Position);
            Assert.IsTrue(origins[0].IsGroundFound);
        }
    }
}
