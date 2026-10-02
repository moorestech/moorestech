using Core.BeltTransport;
using NUnit.Framework;

namespace Tests.UnitTest.Core.BeltTransport
{
    public class BeltPriorityTest
    {
        [TestCase(BeltDirection.Front, 0, 2, 3)]
        [TestCase(BeltDirection.Back, 1, 2, 3)]
        [TestCase(BeltDirection.Left, 2, 0, 1)]
        [TestCase(BeltDirection.Right, 3, 0, 1)]
        public void 初期順は直進が先頭で横2方向が方向番号順(BeltDirection straight, int first, int second, int third)
        {
            var order = BeltPriority.Create(straight);
            AssertOrder(order, first, second, third);
        }

        [Test]
        public void 成功した方向を末尾へ移し残りの相対順序を保つ()
        {
            // READMEの表: (0,1,2)+0→(1,2,0)、(1,2,0)+2→(1,0,2)、(1,0,2)+2→不変
            // README table: (0,1,2)+0→(1,2,0), (1,2,0)+2→(1,0,2), (1,0,2)+2→unchanged
            var order = MakeOrder(0, 1, 2);
            order = BeltPriority.MoveLast(order, 0);
            AssertOrder(order, 1, 2, 0);

            order = BeltPriority.MoveLast(order, 2);
            AssertOrder(order, 1, 0, 2);

            var unchanged = BeltPriority.MoveLast(order, 2);
            Assert.AreEqual(order, unchanged);
        }

        [Test]
        public void 未接続方向を飛ばして最優先の接続済み方向を返す()
        {
            var order = MakeOrder(0, 2, 3);
            Assert.AreEqual(0, BeltPriority.FirstConnected(order, (1 << 0) | (1 << 3)));
            Assert.AreEqual(3, BeltPriority.FirstConnected(order, 1 << 3));
            Assert.AreEqual(2, BeltPriority.FirstConnected(order, (1 << 2) | (1 << 3)));

            // 候補外の方向（1）だけが接続されていても選ばない
            // A connected direction outside the three candidates (1) is never chosen
            Assert.AreEqual(-1, BeltPriority.FirstConnected(order, 1 << 1));
            Assert.AreEqual(-1, BeltPriority.FirstConnected(order, 0));
        }

        private static int MakeOrder(int first, int second, int third)
        {
            return first | (second << 2) | (third << 4);
        }

        private static void AssertOrder(int order, int first, int second, int third)
        {
            Assert.AreEqual(first, BeltPriority.Direction(order, 0), "rank0");
            Assert.AreEqual(second, BeltPriority.Direction(order, 1), "rank1");
            Assert.AreEqual(third, BeltPriority.Direction(order, 2), "rank2");
            Assert.AreEqual(0, order >> 6, "6bitを超えた位置にbitが残っている");
        }
    }
}
