
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
            Assert.AreEqual(ItemConst.NullItemId,result.Id);
            
            result = playerInventory.InsertItem(0,new ItemStack(id,amount));
            Assert.AreEqual(id,result.Id);
            Assert.AreEqual(amount,result.Amount);
            
            result = playerInventory.InsertItem(0,new ItemStack(id+1,1));
            Assert.AreEqual(id + 1,result.Id);
            Assert.AreEqual(1,result.Amount);

            //drop and inset item test
            result = playerInventory.DropItem(0, 3);
            Assert.AreEqual(id,result.Id);
            Assert.AreEqual(3,result.Amount);
            
            result = playerInventory.GetItem(0);
            Assert.AreEqual(id,result.Id);
            Assert.AreEqual(amount - 3,result.Amount);
            
            result = playerInventory.InsertItem(0,new ItemStack(id,amount));
            Assert.AreEqual(id,result.Id);
            Assert.AreEqual(amount - 3,result.Amount);
            
            result = playerInventory.DropItem(0, amount - 3);
            Assert.AreEqual(id,result.Id);
            Assert.AreEqual(amount - 3,result.Amount);
            


        }
    }
}