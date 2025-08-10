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
    }
}