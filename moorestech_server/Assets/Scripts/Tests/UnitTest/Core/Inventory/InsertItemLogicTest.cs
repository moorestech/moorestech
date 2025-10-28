using Core.Inventory;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Inventory
{
    /// <summary>
    ///     <see cref="InventoryInsertItem" /> をテストする
    /// </summary>
    public class InsertItemLogicTest
    {
        [Test]
        public void InsertItemWithPrioritySlotTest()
        {
            var (_, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var toInventory = new OpenableInventoryItemDataStoreService((_, _) => { }, itemStackFactory, 10);
            
            
            // 8,9番目のスロットに優先的にアイテムを入れるようにする
            var insertItem = itemStackFactory.Create(new ItemId(1), 10);
            toInventory.InsertItemWithPrioritySlot(insertItem, new[] { 8, 9 });
            //8番目に入っているか確認
            Assert.AreEqual(insertItem, toInventory.GetItem(8));
            
            //ID2を入れて、9番目に入っているかを確認する
            insertItem = itemStackFactory.Create(new ItemId(2), 10);
            toInventory.InsertItemWithPrioritySlot(insertItem, new[] { 8, 9 });
            //9番目に入っているか確認
            Assert.AreEqual(insertItem, toInventory.GetItem(9));
            
            //ID3を入れて、8,9番目に入らないので0番目に入ることを確認する
            insertItem = itemStackFactory.Create(new ItemId(3), 10);
            toInventory.InsertItemWithPrioritySlot(insertItem, new[] { 8, 9 });
            //0番目に入っているか確認
            Assert.AreEqual(insertItem, toInventory.GetItem(0));
        }

        [Test]
        public void AllowMultipleStacksTest_DefaultBehavior()
        {
            var (_, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemStackFactory = ServerContext.ItemStackFactory;
            var toInventory = new OpenableInventoryItemDataStoreService((_, _) => { }, itemStackFactory, 10);

            // スロット0にItemId(1)を80個挿入（maxStack: 100）
            toInventory.InsertItem(itemStackFactory.Create(new ItemId(1), 80));
            Assert.AreEqual(80, toInventory.GetItem(0).Count);

            // 同じItemId(1)を40個挿入
            toInventory.InsertItem(itemStackFactory.Create(new ItemId(1), 40));

            // 期待結果: スロット0が100個（満タン）、スロット1に20個の新しいスタックが作成される
            Assert.AreEqual(100, toInventory.GetItem(0).Count);
            Assert.AreEqual(new ItemId(1), toInventory.GetItem(0).Id);
            Assert.AreEqual(20, toInventory.GetItem(1).Count);
            Assert.AreEqual(new ItemId(1), toInventory.GetItem(1).Id);
        }

        [Test]
        public void InsertDefaultBehavior_OverflowGoesToNearestSlot()
        {
            var (_, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemStackFactory = ServerContext.ItemStackFactory;
            var toInventory = new OpenableInventoryItemDataStoreService((_, _) => { }, itemStackFactory, 10);

            // 既存スタックを作成しテスト環境を整える
            // Prepare inventory with an existing stack for testing
            toInventory.SetItem(5, itemStackFactory.Create(new ItemId(1), 80));

            // 追加投入で溢れを発生させる
            // Insert more items to trigger overflow
            var remain = toInventory.InsertItem(itemStackFactory.Create(new ItemId(1), 40));

            // 既存スロットが満杯になり隣接スロットへ展開したことを確認
            // Ensure the primary slot is full and overflow moved to the nearest slot
            Assert.AreEqual(100, toInventory.GetItem(5).Count);
            Assert.AreEqual(new ItemId(1), toInventory.GetItem(5).Id);
            Assert.AreEqual(20, toInventory.GetItem(4).Count);
            Assert.AreEqual(new ItemId(1), toInventory.GetItem(4).Id);
            Assert.IsTrue(remain.Equals(itemStackFactory.CreatEmpty()));
        }

        [Test]
        public void AllowMultipleStacksTest_FalseAddToExistingStack()
        {
            var (_, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemStackFactory = ServerContext.ItemStackFactory;
            var option = new OpenableInventoryItemDataStoreServiceOption { AllowMultipleStacksPerItemOnInsert = false };
            var toInventory = new OpenableInventoryItemDataStoreService((_, _) => { }, itemStackFactory, 10, option);

            // スロット0にItemId(1)を30個挿入
            toInventory.InsertItem(itemStackFactory.Create(new ItemId(1), 30));
            Assert.AreEqual(30, toInventory.GetItem(0).Count);

            // 同じItemId(1)を20個挿入
            var remainItem = toInventory.InsertItem(itemStackFactory.Create(new ItemId(1), 20));

            // 期待結果: スロット0が50個、他のスロットは空（新しいスタック作成なし）
            Assert.AreEqual(50, toInventory.GetItem(0).Count);
            Assert.AreEqual(new ItemId(1), toInventory.GetItem(0).Id);
            Assert.IsTrue(toInventory.GetItem(1).Equals(itemStackFactory.CreatEmpty()));
            Assert.IsTrue(remainItem.Equals(itemStackFactory.CreatEmpty()));
        }

        [Test]
        public void AllowMultipleStacksTest_FalseWithFullStack()
        {
            var (_, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemStackFactory = ServerContext.ItemStackFactory;
            var option = new OpenableInventoryItemDataStoreServiceOption { AllowMultipleStacksPerItemOnInsert = false };
            var toInventory = new OpenableInventoryItemDataStoreService((_, _) => { }, itemStackFactory, 10, option);

            // スロット0にItemId(1)を100個（満タン）挿入
            toInventory.InsertItem(itemStackFactory.Create(new ItemId(1), 100));
            Assert.AreEqual(100, toInventory.GetItem(0).Count);

            // 同じItemId(1)を30個挿入し、余りを取得
            var remainItem = toInventory.InsertItem(itemStackFactory.Create(new ItemId(1), 30));

            // 期待結果: スロット0は100個のまま、余りが30個、他のスロットは空
            Assert.AreEqual(100, toInventory.GetItem(0).Count);
            Assert.AreEqual(new ItemId(1), toInventory.GetItem(0).Id);
            Assert.IsTrue(toInventory.GetItem(1).Equals(itemStackFactory.CreatEmpty()));
            Assert.AreEqual(30, remainItem.Count);
            Assert.AreEqual(new ItemId(1), remainItem.Id);
        }

        [Test]
        public void AllowMultipleStacksTest_FalsePartialInsert()
        {
            var (_, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemStackFactory = ServerContext.ItemStackFactory;
            var option = new OpenableInventoryItemDataStoreServiceOption { AllowMultipleStacksPerItemOnInsert = false };
            var toInventory = new OpenableInventoryItemDataStoreService((_, _) => { }, itemStackFactory, 10, option);

            // スロット0にItemId(1)を80個挿入
            toInventory.InsertItem(itemStackFactory.Create(new ItemId(1), 80));
            Assert.AreEqual(80, toInventory.GetItem(0).Count);

            // 同じItemId(1)を40個挿入し、余りを取得
            var remainItem = toInventory.InsertItem(itemStackFactory.Create(new ItemId(1), 40));

            // 期待結果: スロット0が100個（満タン）、余りが20個、他のスロットは空（新しいスタック作成なし）
            Assert.AreEqual(100, toInventory.GetItem(0).Count);
            Assert.AreEqual(new ItemId(1), toInventory.GetItem(0).Id);
            Assert.IsTrue(toInventory.GetItem(1).Equals(itemStackFactory.CreatEmpty()));
            Assert.AreEqual(20, remainItem.Count);
            Assert.AreEqual(new ItemId(1), remainItem.Id);
        }

        [Test]
        public void AllowMultipleStacksTest_FalseFirstStackCreation()
        {
            var (_, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemStackFactory = ServerContext.ItemStackFactory;
            var option = new OpenableInventoryItemDataStoreServiceOption { AllowMultipleStacksPerItemOnInsert = false };
            var toInventory = new OpenableInventoryItemDataStoreService((_, _) => { }, itemStackFactory, 10, option);

            // 空のインベントリにItemId(1)を50個挿入
            var remainItem = toInventory.InsertItem(itemStackFactory.Create(new ItemId(1), 50));

            // 期待結果: スロット0に50個が作成される（最初のスタックなので作成OK）
            Assert.AreEqual(50, toInventory.GetItem(0).Count);
            Assert.AreEqual(new ItemId(1), toInventory.GetItem(0).Id);
            Assert.IsTrue(remainItem.Equals(itemStackFactory.CreatEmpty()));
        }

        [Test]
        public void AllowMultipleStacksTest_FalseWithMultipleExistingStacks()
        {
            var (_, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemStackFactory = ServerContext.ItemStackFactory;
            var option = new OpenableInventoryItemDataStoreServiceOption { AllowMultipleStacksPerItemOnInsert = false };
            var toInventory = new OpenableInventoryItemDataStoreService((_, _) => { }, itemStackFactory, 10, option);

            // SetItemでスロット0にItemId(1)を60個、スロット2にItemId(1)を70個を配置
            toInventory.SetItem(0, itemStackFactory.Create(new ItemId(1), 60));
            toInventory.SetItem(2, itemStackFactory.Create(new ItemId(1), 70));

            // 同じItemId(1)を80個挿入し、余りを取得
            var remainItem = toInventory.InsertItem(itemStackFactory.Create(new ItemId(1), 80));

            // 期待結果: スロット0が100個、スロット2が100個、余り10個、スロット1は空（新しいスタック作成なし）
            Assert.AreEqual(100, toInventory.GetItem(0).Count);
            Assert.AreEqual(new ItemId(1), toInventory.GetItem(0).Id);
            Assert.IsTrue(toInventory.GetItem(1).Equals(itemStackFactory.CreatEmpty()));
            Assert.AreEqual(100, toInventory.GetItem(2).Count);
            Assert.AreEqual(new ItemId(1), toInventory.GetItem(2).Id);
            Assert.AreEqual(10, remainItem.Count);
            Assert.AreEqual(new ItemId(1), remainItem.Id);
        }

        [Test]
        public void InsertItemWithPrioritySlot_OverflowRespectsNearestSlot()
        {
            var (_, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemStackFactory = ServerContext.ItemStackFactory;
            var toInventory = new OpenableInventoryItemDataStoreService((_, _) => { }, itemStackFactory, 10);

            // 優先スロット内に既存スタックを配置する
            // Place an existing stack within priority slots
            toInventory.SetItem(7, itemStackFactory.Create(new ItemId(1), 90));

            // 過剰投入で隣接スロットへの展開を検証する
            // Insert enough items to validate the overflow spreading behavior
            var remain = toInventory.InsertItemWithPrioritySlot(itemStackFactory.Create(new ItemId(1), 20), new[] { 7, 2 });

            // 優先スロットが満杯になり別の優先スロットへ展開したことを確認
            // Ensure the primary priority slot is full and overflow moved to the next priority slot
            Assert.AreEqual(100, toInventory.GetItem(7).Count);
            Assert.AreEqual(new ItemId(1), toInventory.GetItem(7).Id);
            Assert.AreEqual(10, toInventory.GetItem(2).Count);
            Assert.AreEqual(new ItemId(1), toInventory.GetItem(2).Id);
            Assert.IsTrue(remain.Equals(itemStackFactory.CreatEmpty()));
        }
    }
}
