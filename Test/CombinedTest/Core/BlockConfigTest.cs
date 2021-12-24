using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using NUnit.Framework;
using Test.Module.TestConfig;

namespace Test.CombinedTest.Core
{
    public class BlockConfigTest
    {
        [TestCase(1,"TestMachine1")]
        [TestCase(2,"aaa")]
        [TestCase(3,"bb")]
        [TestCase(4,"ccccc")]
        public void ConfigNameTest(int id,string ans)
        {
            string name = new AllMachineBlockConfig().GetBlockConfig(id).Name;
            Assert.AreEqual(ans,name);
        }

        
        [TestCase(1,2)]
        [TestCase(2,5)]
        [TestCase(3,22)]
        [TestCase(4,25)]
        public void InputSlotsTest(int id, int ans)
        {
            int slots = ((MachineBlockConfigParam)new AllMachineBlockConfig().GetBlockConfig(id).Param).InputSlot;
            Assert.AreEqual(ans,slots);
        }
        [TestCase(1,1)]
        [TestCase(2,3)]
        [TestCase(3,1)]
        [TestCase(4,5)]
        public void OutputSlotsTest(int id, int ans)
        {
            int slots = ((MachineBlockConfigParam)new AllMachineBlockConfig().GetBlockConfig(id).Param).OutputSlot;
            Assert.AreEqual(ans,slots);
        }
    }
}