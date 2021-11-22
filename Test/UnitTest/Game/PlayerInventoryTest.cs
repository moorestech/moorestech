
using Core.Config.Item;
using Core.Item;
using NUnit.Framework;
using PlayerInventory;

namespace Test.UnitTest.Game
{
    public class PlayerInventoryTest
    {
        [Test]
        public void InsertToGetTest()
        {
            var playerInventory = new PlayerInventoryData(0);
            int id = 5;
            var amount = ItemConfig.GetItemConfig(id).Stack;
            //Insert test
            var result = playerInventory.InsertItem(0,new ItemStack(id,amount));
            Assert.AreEqual(result.Id,ItemConst.NullItemId);
            result = playerInventory.InsertItem(0,new ItemStack(id,amount));
            Assert.AreEqual(result.Id,id);
            Assert.AreEqual(result.Amount,amount);
            result = playerInventory.InsertItem(0,new ItemStack(id+1,1));
            Assert.AreEqual(result.Id,id+1);
            Assert.AreEqual(result.Amount,1);

            //drop and inset item test
            result = playerInventory.DropItem(0, 3);
            Assert.AreEqual(result.Id,id);
            Assert.AreEqual(result.Amount,3);
            result = playerInventory.GetItem(0);
            Assert.AreEqual(result.Id,id);
            Assert.AreEqual(result.Amount,amount - 3);
            result = playerInventory.InsertItem(0,new ItemStack(id,amount));
            Assert.AreEqual(result.Id,id);
            Assert.AreEqual(result.Amount,amount - 3);
            result = playerInventory.DropItem(0, amount - 3);
            Assert.AreEqual(result.Id,id);
            Assert.AreEqual(result.Amount,amount - 3);
            result = playerInventory.GetItem(0);
            Assert.AreEqual(result.Id,ItemConst.NullItemId);
            


        }
    }
}