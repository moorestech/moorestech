using System;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using NUnit.Framework;

namespace Tests.UnitTest.Core.BeltTransport
{
    public class BeltDirectionTest
    {
        [TestCase(BeltDirection.Front, BeltDirection.Back)]
        [TestCase(BeltDirection.Back, BeltDirection.Front)]
        [TestCase(BeltDirection.Left, BeltDirection.Right)]
        [TestCase(BeltDirection.Right, BeltDirection.Left)]
        public void 反対方向を返す(BeltDirection direction, BeltDirection expected)
        {
            Assert.AreEqual(expected, BeltDirections.Opposite(direction));
        }

        [Test]
        public void 搬入元方向は12通りで4bitに収まる()
        {
            var values = (BeltEntryDirection[])Enum.GetValues(typeof(BeltEntryDirection));
            Assert.AreEqual(12, values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                Assert.AreEqual(i, (int)values[i]);
                Assert.AreEqual(0, (int)values[i] & ~0xF);
            }
        }

        [Test]
        public void WithEntryDirectionは搬入元方向だけを差し替える()
        {
            var item = new BeltItem(new ItemId(5), new ItemInstanceId(123456789L), BeltEntryDirection.FromLeft);
            var moved = item.WithEntryDirection(BeltEntryDirection.FromBackBelow);

            Assert.AreEqual(item.ItemId, moved.ItemId);
            Assert.AreEqual(item.ItemInstanceId, moved.ItemInstanceId);
            Assert.AreEqual(BeltEntryDirection.FromBackBelow, moved.EntryDirection);
            Assert.AreEqual(BeltEntryDirection.FromLeft, item.EntryDirection);
        }
    }
}
