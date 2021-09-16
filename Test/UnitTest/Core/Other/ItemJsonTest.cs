using Core.Config.Item;
using NUnit.Framework;

namespace Test.UnitTest.Core.Other
{
    public class ItemJsonTest
    {
        [TestCase(0,100)]
        [TestCase(1,50)]
        [TestCase(2,300)]
        [TestCase(3,50)]
        [TestCase(4,200)]
        [TestCase(5,10)]
        public void JsonStackTest(int id, int stack)
        {
            Assert.AreEqual(stack, ItemConfig.GetItemConfig(id).Stack);
        }
        [TestCase(0,"Test1")]
        [TestCase(1,"Test2")]
        [TestCase(2,"Test3")]
        [TestCase(3,"Test4")]
        [TestCase(4,"Test5")]
        [TestCase(5,"Test6")]
        public void JsonNameTest(int id, string name)
        {
            Assert.AreEqual(name, ItemConfig.GetItemConfig(id).Name);
        }
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void JsonIdTest(int id)
        {
            Assert.AreEqual(id, ItemConfig.GetItemConfig(id).Id);
        }
    }
}