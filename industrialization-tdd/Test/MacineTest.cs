using System;
using industrialization.Installation.BeltConveyor;
using industrialization.Installation.Machine;
using industrialization.Item;
using NUnit.Framework;

namespace industrialization.Test
{
    public class MacineTest
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            int ID = 10;
            var machine = new Macine(0,Guid.Empty,new BeltConveyor(0, Guid.Empty));
            machine.InsertItem(new ItemStack(10,1));
            var outputItem = machine.GetInventory().ItemStacks[1].ID;
            
            Assert.AreEqual(ID,outputItem);
        }
    }
}