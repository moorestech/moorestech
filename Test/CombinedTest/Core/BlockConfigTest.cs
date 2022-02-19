using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Test.Module.TestConfig;

namespace Test.CombinedTest.Core
{
    [TestClass]
    public class BlockConfigTest
    {

        [TestMethod]
        public void ConfigNameTest()
        {
            ConfigNameTest(1, "TestMachine1");
            ConfigNameTest(2, "aaa");
            ConfigNameTest(3, "bb");
            ConfigNameTest(4, "ccccc");
        }
        
        public void ConfigNameTest(int id, string ans)
        {
            string name = new AllMachineBlockConfig().GetBlockConfig(id).Name;
            Assert.AreEqual(ans, name);
        }


        [TestMethod]
        public void InputSlotsTest()
        {
            InputSlotsTest(1, 2);
            InputSlotsTest(2, 5);
            InputSlotsTest(3, 22);
            InputSlotsTest(4, 25);
        }
        public void InputSlotsTest(int id, int ans)
        {
            int slots = ((MachineBlockConfigParam) new AllMachineBlockConfig().GetBlockConfig(id).Param).InputSlot;
            Assert.AreEqual(ans, slots);
        }


        [TestMethod]
        public void OutputSlotsTest()
        {
            OutputSlotsTest(1, 1);
            OutputSlotsTest(2, 3);
            OutputSlotsTest(3, 1);
            OutputSlotsTest(4, 5);
        }
        public void OutputSlotsTest(int id, int ans)
        {
            int slots = ((MachineBlockConfigParam) new AllMachineBlockConfig().GetBlockConfig(id).Param).OutputSlot;
            Assert.AreEqual(ans, slots);
        }
    }
}