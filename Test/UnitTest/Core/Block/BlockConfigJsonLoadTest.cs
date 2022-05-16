using Core.Block.Config;
using Core.Block.Config.LoadConfig;
using Core.Block.Config.LoadConfig.Param;
using NUnit.Framework;
using Test.Module.TestConfig;

namespace Test.UnitTest.Core.Block
{
    public class BlockConfigJsonLoadTest
    {
        /// <summary>
        /// Unit Test Block Config.jsonに基づいてテストコードが書かれています。
        /// 仕様を追加したら、このテストコードを更新してください。
        /// </summary>
        [Test]
        public void JsonLoadTest()
        {
            var data = new BlockConfigJsonLoad().LoadFromJsons(TestModuleConfig.UnitTestBlockConfigJson);

            Assert.AreEqual(data[0].BlockId, 1);
            Assert.AreEqual(data[0].Name, "TestMachine1");
            Assert.AreEqual(data[0].Type, VanillaBlockType.Machine);
            Assert.AreEqual(((MachineBlockConfigParam) data[0].Param).InputSlot, 2);
            Assert.AreEqual(((MachineBlockConfigParam) data[0].Param).OutputSlot, 1);
            Assert.AreEqual(((MachineBlockConfigParam) data[0].Param).RequiredPower, 100);

            Assert.AreEqual(data[1].BlockId, 2);
            Assert.AreEqual(data[1].Name, "TestMachine2");
            Assert.AreEqual(data[1].Type, VanillaBlockType.Machine);
            Assert.AreEqual(((MachineBlockConfigParam) data[1].Param).InputSlot, 3);
            Assert.AreEqual(((MachineBlockConfigParam) data[1].Param).OutputSlot, 1);
            Assert.AreEqual(((MachineBlockConfigParam) data[1].Param).RequiredPower, 100);

            Assert.AreEqual(data[2].BlockId, 3);
            Assert.AreEqual(data[2].Name, "TestMachine3");
            Assert.AreEqual(data[2].Type, VanillaBlockType.Machine);
            Assert.AreEqual(((MachineBlockConfigParam) data[2].Param).InputSlot, 2);
            Assert.AreEqual(((MachineBlockConfigParam) data[2].Param).OutputSlot, 3);
        }
    }
}