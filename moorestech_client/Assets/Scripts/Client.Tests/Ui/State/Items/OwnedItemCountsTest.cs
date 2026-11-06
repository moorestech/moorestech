using System.Collections.Generic;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Items
{
    public class OwnedItemCountsTest
    {
        private static SlotItem S(int itemId, int count) => new(new ItemId(itemId), count);
        private static ItemId Id(int itemId) => new(itemId);

        private static readonly SlotItem[] Unowned = { S(-1, 5), S(0, 5), S(7, 0), S(7, -3) };

        /// <web-test file="src/shared/ownedCounts.test.ts">同一 itemId を跨スロットで合算する</web-test>
        [Test]
        public void 同一アイテムをスロットをまたいで合算する()
        {
            var owned = OwnedItemCounts.Build(new[] { S(1, 10), S(2, 3), S(1, 5) });
            Assert.AreEqual(15, owned[Id(1)]);
            Assert.AreEqual(3, owned[Id(2)]);
        }

        /// <web-test file="src/shared/ownedCounts.test.ts">空スロット(itemId=0)は無視する</web-test>
        [Test]
        public void 空スロットは無視する()
        {
            Assert.AreEqual(0, OwnedItemCounts.Build(new[] { S(0, 0) }).Count);
        }

        /// <web-test file="src/shared/ownedCounts.test.ts">count&lt;=0 のスロットはエントリを作らない（旧recipe実装と同挙動）</web-test>
        [Test]
        public void 個数0のスロットはエントリを作らない()
        {
            Assert.IsFalse(OwnedItemCounts.Build(new[] { S(5, 0) }).ContainsKey(Id(5)));
        }

        /// <web-test file="src/shared/ownedCounts.test.ts">全素材を満たせば true</web-test>
        [Test]
        public void 全素材を満たせば充足する()
        {
            var owned = new Dictionary<ItemId, int> { { Id(1), 3 } };
            Assert.IsTrue(OwnedItemCounts.HasEnoughItems(new[] { new ItemAmount(Id(1), 3) }, owned));
        }

        /// <web-test file="src/shared/ownedCounts.test.ts">一つでも不足なら false</web-test>
        [Test]
        public void 一つでも不足なら充足しない()
        {
            var owned = new Dictionary<ItemId, int> { { Id(1), 2 } };
            Assert.IsFalse(OwnedItemCounts.HasEnoughItems(new[] { new ItemAmount(Id(1), 3) }, owned));
        }

        /// <web-test file="src/shared/ownedCounts.test.ts">maps each item to its first slot and skips empty slots</web-test>
        [Test]
        public void 各アイテムを先頭スロットへ対応させ空スロットは飛ばす()
        {
            var first = OwnedItemCounts.FirstSlotIndexByItemId(new[] { S(0, 0), S(7, 3), S(3, 1), S(7, 5) });
            Assert.AreEqual(2, first.Count);
            Assert.AreEqual(1, first[Id(7)]);
            Assert.AreEqual(2, first[Id(3)]);
        }

        /// <web-test file="src/shared/ownedCounts.test.ts">skips a count=0 slot even if it appears before a real stack of the same item</web-test>
        [Test]
        public void 同じアイテムの個数0のスロットは先頭にあっても飛ばす()
        {
            var first = OwnedItemCounts.FirstSlotIndexByItemId(new[] { S(0, 0), S(7, 0), S(3, 1), S(7, 5) });
            Assert.AreEqual(2, first.Count);
            Assert.AreEqual(2, first[Id(3)]);
            Assert.AreEqual(3, first[Id(7)]);
        }

        /// <web-test file="src/shared/ownedCounts.test.ts">負のitemId/countを所持外とする</web-test>
        [Test]
        public void 負のアイテムや個数は所持外とする()
        {
            foreach (var slot in Unowned) Assert.IsFalse(OwnedItemCounts.IsOwnedSlot(slot), slot.ToString());
        }

        /// <web-test file="src/shared/ownedCounts.test.ts">集計も先頭スロット探索も所持外スロットを一切拾わない</web-test>
        [Test]
        public void 集計も先頭スロット探索も所持外スロットを拾わない()
        {
            Assert.AreEqual(0, OwnedItemCounts.Build(Unowned).Count);
            Assert.AreEqual(0, OwnedItemCounts.FirstSlotIndexByItemId(Unowned).Count);
        }
    }
}
