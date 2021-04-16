using System;
using industrialization.Installation;
using industrialization.Installation.BeltConveyor;
using industrialization.Item;
using NUnit.Framework;

namespace industrialization.Test
{
    public class BeltConveyorTest
    {
        [Test]
        public void InsertBeltConveyorTest()
        {
            var random = new Random(4123);
            for (int i = 0; i < 20; i++)
            {
                //必要な変数を作成
                int id = random.Next(0, 10);
                int amount = random.Next(0, 10);
                var item = ItemStackFactory.NewItemStack(id, amount);
                var beltconveyor = new BeltConveyor(0, new Guid(),new NullIInstallationInventory());

                var outputItem = beltconveyor.InsertItem(item);
                
                Assert.True(outputItem.Equals(ItemStackFactory.NewItemStack(id,amount-1)));
            }
        }
    }
}