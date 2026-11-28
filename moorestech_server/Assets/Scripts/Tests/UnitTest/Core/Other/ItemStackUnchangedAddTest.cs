using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Other
{
    // 1個も入らない加算は、双方の個体を作り直さずそのまま返す（詰まった搬出が毎tick試すため、作り直すとGCを呼ぶ）
    // An addition that takes nothing returns both instances as they are (blocked outputs retry every tick, and recreating them feeds the GC)
    public class ItemStackUnchangedAddTest
    {
        private IItemStackFactory _itemStackFactory;

        [SetUp]
        public void Setup()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _itemStackFactory = ServerContext.ItemStackFactory;
        }

        [Test]
        public void 満杯のスタックへの加算は双方の個体をそのまま返す()
        {
            var itemId = ForUnitTestItemId.ItemId1;
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(itemId);
            var full = _itemStackFactory.Create(itemId, maxStack);
            var incoming = _itemStackFactory.Create(itemId, 1);

            var result = full.AddItem(incoming);

            Assert.AreSame(full, result.ProcessResultItemStack);
            Assert.AreSame(incoming, result.RemainderItemStack);
        }

        [Test]
        public void 種類の違うアイテムの加算は双方の個体をそのまま返す()
        {
            var existing = _itemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
            var incoming = _itemStackFactory.Create(ForUnitTestItemId.ItemId2, 1);

            var result = existing.AddItem(incoming);

            Assert.AreSame(existing, result.ProcessResultItemStack);
            Assert.AreSame(incoming, result.RemainderItemStack);
        }

        [Test]
        public void 空きのあるスタックへの加算は新しい個体を返す()
        {
            // 中身が変わる加算は従来どおり新しい個体になる（同じ個体IDが2か所に載らないこと）
            // An addition that changes the content still yields a new instance, so one instance id never sits in two places
            var itemId = ForUnitTestItemId.ItemId1;
            var existing = _itemStackFactory.Create(itemId, 1);
            var incoming = _itemStackFactory.Create(itemId, 1);

            var result = existing.AddItem(incoming);

            Assert.AreEqual(2, result.ProcessResultItemStack.Count);
            Assert.AreNotEqual(existing.ItemInstanceId, result.ProcessResultItemStack.ItemInstanceId);
            Assert.AreEqual(ItemMaster.EmptyItemId, result.RemainderItemStack.Id);
        }

        [Test]
        public void 属性を足しても属性の無い別のアイテムへ漏れない()
        {
            // 属性の無いアイテムは空の辞書を共有するので、SetMetaが共有物を書き換えないことを固定する
            // Items without metadata share one empty dictionary, so pin that SetMeta never writes into the shared one
            var plain = _itemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
            var other = _itemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);

            var withMeta = plain.SetMeta("test1", new TestMeta1());

            Assert.IsTrue(withMeta.TryGetMeta("test1", out _));
            Assert.IsFalse(plain.TryGetMeta("test1", out _));
            Assert.IsFalse(other.TryGetMeta("test1", out _));
            Assert.IsTrue(plain.Equals(other));
            Assert.IsFalse(plain.Equals(withMeta));
        }
    }
}
