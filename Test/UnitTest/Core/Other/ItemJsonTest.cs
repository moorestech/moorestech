using Core.Config.Item;
using Core.ConfigJson;
using Core.Item.Config;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.StartServerSystem;
using Test.Module.TestConfig;
using Test.Module.TestMod;

namespace Test.UnitTest.Core.Other
{
    public class ItemJsonTest
    {
        [TestCase(0, 100)]
        [TestCase(1, 50)]
        [TestCase(2, 300)]
        [TestCase(3, 55)]
        [TestCase(4, 200)]
        [TestCase(5, 30)]
        public void JsonStackTest(int id, int stack)
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemConfig = serviceProvider.GetService<IItemConfig>();
            Assert.AreEqual(stack, itemConfig.GetItemConfig(id).MaxStack);
        }

        [TestCase(0, "Test1")]
        [TestCase(1, "Test2")]
        [TestCase(2, "Test3")]
        [TestCase(3, "Test4")]
        [TestCase(4, "Test5")]
        [TestCase(5, "Test6")]
        public void JsonNameTest(int id, string name)
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemConfig = serviceProvider.GetService<IItemConfig>();
            Assert.AreEqual(name, itemConfig.GetItemConfig(id).Name);
        }
    }
}