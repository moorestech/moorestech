using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Other
{
    public class ItemConfigTest
    {
        [TestCase(1, 100)]
        [TestCase(2, 50)]
        [TestCase(3, 300)]
        [TestCase(4, 100)]
        [TestCase(5, 200)]
        [TestCase(7, 100)]
        public void JsonStackTest(int id, int stack)
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.AreEqual(stack, MasterHolder.ItemMaster.GetItemMaster(new ItemId(id)).MaxStack);
        }
        
        [TestCase(1, "Test1")]
        [TestCase(2, "Test2")]
        [TestCase(3, "Test3")]
        [TestCase(4, "Test4")]
        [TestCase(5, "Test5")]
        [TestCase(6, "Test6")]
        [TestCase(7, "Test7")]
        public void JsonNameTest(int id, string name)
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.AreEqual(name, MasterHolder.ItemMaster.GetItemMaster(new ItemId(id)).Name);
        }
    }
}