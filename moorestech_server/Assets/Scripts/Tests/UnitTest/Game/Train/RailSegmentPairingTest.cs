using Game.Train.RailGraph.Utility;
using NUnit.Framework;

namespace Tests.UnitTest.Game
{
    // 物理レール1本の向き違い2区間が同じキーになり、別のレールとは衝突しないことを確かめる
    // Both directed edges of one physical rail map to one key, and different rails never collide
    public class RailSegmentPairingTest
    {
        [Test]
        public void 向き違いの対は同じキーになる()
        {
            // A=4→B=7の対は6→5
            // The pair of A=4→B=7 is 6→5
            Assert.AreEqual((4, 7), RailSegmentPairing.SelectCanonicalPair(4, 7));
            Assert.AreEqual((4, 7), RailSegmentPairing.SelectCanonicalPair(6, 5));
        }

        [Test]
        public void 起点が同値の自己対は自分自身を返す()
        {
            // from == to^1 のとき対は自分と同じ区間になる
            // When from == to^1 the pair is the same edge
            Assert.AreEqual((2, 3), RailSegmentPairing.SelectCanonicalPair(2, 3));
        }

        [Test]
        public void 別のレールは別のキーになる()
        {
            Assert.AreNotEqual(RailSegmentPairing.SelectCanonicalPair(4, 7), RailSegmentPairing.SelectCanonicalPair(4, 9));
        }
    }
}
