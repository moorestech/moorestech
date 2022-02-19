using Core.Config.Item;
using Core.Item.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.UnitTest.Core.Other
{
    [TestClass]
    public class ItemJsonTest
    {
        [TestMethod]
        public void JsonStackTest()
        {
            JsonStackTest(0, 100);
            JsonStackTest(1, 50);
            JsonStackTest(2, 300);
            JsonStackTest(3, 55);
            JsonStackTest(4, 200);
            JsonStackTest(5, 30);
        }
        public void JsonStackTest(int id, int stack)
        {
            Assert.AreEqual(stack, new TestItemConfig().GetItemConfig(id).MaxStack);
        }

        [TestMethod]
        public void JsonNameTest()
        {
            JsonNameTest(0, "Test1");
            JsonNameTest(1, "Test2");
            JsonNameTest(2, "Test3");
            JsonNameTest(3, "Test4");
            JsonNameTest(4, "Test5");
            JsonNameTest(5, "Test6");
        }
        public void JsonNameTest(int id, string name)
        {
            Assert.AreEqual(name, new TestItemConfig().GetItemConfig(id).Name);
        }
        [TestMethod]
        public void JsonIdTest()
        {
            JsonIdTest(0);
            JsonIdTest(1);
            JsonIdTest(2);
            JsonIdTest(3);
            JsonIdTest(4);
            JsonIdTest(5);
        }
        public void JsonIdTest(int id)
        {
            Assert.AreEqual(id, new TestItemConfig().GetItemConfig(id).Id);
        }
    }
}