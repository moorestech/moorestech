using Game.Block.Interface;
using Game.Block.Interface.Extension;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class BlockPositionInfoVeinOverlapTest
    {
        private static readonly Vector3Int VeinMin = new(0, 0, 0);
        private static readonly Vector3Int VeinMax = new(2, 2, 2);

        [Test]
        public void フットプリントが1セルでもXZで重なれば可()
        {
            // 2x1x2を(2,0,2)原点に置くと(2..3, 2..3)でAABBの角1セルに重なる
            // A 2x1x2 at origin (2,0,2) covers (2..3, 2..3) and touches one corner cell
            var info = new BlockPositionInfo(new Vector3Int(2, 0, 2), BlockDirection.North, new Vector3Int(2, 1, 2));
            Assert.IsTrue(info.OverlapsVeinXz(VeinMin, VeinMax));
        }

        [Test]
        public void XZが隣接しているだけなら不可()
        {
            var info = new BlockPositionInfo(new Vector3Int(3, 0, 0), BlockDirection.North, new Vector3Int(2, 1, 2));
            Assert.IsFalse(info.OverlapsVeinXz(VeinMin, VeinMax));
        }

        [Test]
        public void Yが外れていてもXZが重なれば可()
        {
            var info = new BlockPositionInfo(new Vector3Int(0, 10, 0), BlockDirection.North, new Vector3Int(2, 1, 2));
            Assert.IsTrue(info.OverlapsVeinXz(VeinMin, VeinMax));
        }

        [Test]
        public void 回転後のフットプリントで判定する()
        {
            // 東向き2x1x3は原点から(x:0..2, z:0..1)を占める。原点(-2,0,-1)ならx:-2..0でAABBのx=0に掛かる
            // East-facing 2x1x3 spans (x:0..2, z:0..1) from its origin; origin (-2,0,-1) reaches x=0 of the AABB
            var info = new BlockPositionInfo(new Vector3Int(-2, 0, -1), BlockDirection.East, new Vector3Int(2, 1, 3));
            Assert.IsTrue(info.OverlapsVeinXz(VeinMin, VeinMax));
        }

        [Test]
        public void Z軸プラス側が隣接しているだけなら不可()
        {
            var info = new BlockPositionInfo(new Vector3Int(0, 0, 3), BlockDirection.North, new Vector3Int(2, 1, 2));
            Assert.IsFalse(info.OverlapsVeinXz(VeinMin, VeinMax));
        }

        [Test]
        public void Z軸マイナス側が隣接しているだけなら不可()
        {
            var info = new BlockPositionInfo(new Vector3Int(0, 0, -2), BlockDirection.North, new Vector3Int(2, 1, 2));
            Assert.IsFalse(info.OverlapsVeinXz(VeinMin, VeinMax));
        }
    }
}
