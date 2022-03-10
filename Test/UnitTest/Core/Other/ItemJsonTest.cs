using Core.Config.Item;
using Core.ConfigJson;
using Core.Item.Config;
using NUnit.Framework;
using Test.Module.TestConfig;

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
            Assert.AreEqual(stack, new ItemConfig(new ConfigPath(TestModuleConfigPath.FolderPath)).GetItemConfig(id).MaxStack);
        }

        [TestCase(0, "Test1")]
        [TestCase(1, "Test2")]
        [TestCase(2, "Test3")]
        [TestCase(3, "Test4")]
        [TestCase(4, "Test5")]
        [TestCase(5, "Test6")]
        public void JsonNameTest(int id, string name)
        {
            Assert.AreEqual(name, new ItemConfig(new ConfigPath(TestModuleConfigPath.FolderPath)).GetItemConfig(id).Name);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void JsonIdTest(int id)
        {
            Assert.AreEqual(id, new ItemConfig(new ConfigPath(TestModuleConfigPath.FolderPath)).GetItemConfig(id).Id);
        }
    }
}