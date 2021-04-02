using System;
using System.Threading.Tasks;
using industrialization.Installation.BeltConveyor;
using industrialization.Installation.Machine;
using industrialization.Item;
using industrialization.GameSystem;
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
            GameUpdate.StartUpdate();
            
            Task.Run(exeTest);
        }

        public void exeTest()
        {
            Assert.AreEqual(10,10);
        }
    }
}