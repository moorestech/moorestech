using System;
using System.Threading;
using industrialization.Installation;
using industrialization.Installation.BeltConveyor;
using industrialization.Item;
using NUnit.Framework;

namespace industrialization.Test
{
    public class BeltConveyorTest
    {
        //一個のアイテムが入って正しく搬出されるかのテスト
        [Test]
        public void InsertBeltConveyorTest()
        {
            var random = new Random(4123);
            for (int i = 0; i < 1; i++)
            {
                //必要な変数を作成
                int id = random.Next(0, 10);
                int amount = random.Next(0, 10);
                var item = ItemStackFactory.NewItemStack(id, amount);
                var dummy = new DummyInstallationInventory();
                var beltconveyor = new BeltConveyor(0, new Guid(),dummy);

                var outputItem = beltconveyor.InsertItem(item);
                
                
                Thread.Sleep(2000);
                
                Assert.True(outputItem.Equals(ItemStackFactory.NewItemStack(id,amount-1)));
                Assert.True(dummy.insertedItems[0].Equals(ItemStackFactory.NewItemStack(id,1)));
            }
        }
    }
}