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
            var path = new TestModuleConfigPath().GetPath("Unit Test Block Config.json");
            var data = new BlockConfigJsonLoad().LoadFromJsons(path);

            Assert.AreEqual(data[1].BlockId, 1);
            Assert.AreEqual(data[1].Name, "TestMachine1");
            Assert.AreEqual(data[1].Type, VanillaBlockType.Machine);
            Assert.AreEqual(((MachineBlockConfigParam) data[1].Param).InputSlot, 2);
            Assert.AreEqual(((MachineBlockConfigParam) data[1].Param).OutputSlot, 1);
            Assert.AreEqual(((MachineBlockConfigParam) data[1].Param).RequiredPower, 100);

            Assert.AreEqual(data[2].BlockId, 2);
            Assert.AreEqual(data[2].Name, "TestMachine2");
            Assert.AreEqual(data[2].Type, VanillaBlockType.Machine);
            Assert.AreEqual(((MachineBlockConfigParam) data[2].Param).InputSlot, 3);
            Assert.AreEqual(((MachineBlockConfigParam) data[2].Param).OutputSlot, 1);
            Assert.AreEqual(((MachineBlockConfigParam) data[2].Param).RequiredPower, 100);

            Assert.AreEqual(data[3].BlockId, 3);
            Assert.AreEqual(data[3].Name, "TestMachine3");
            Assert.AreEqual(data[3].Type, VanillaBlockType.Machine);
            Assert.AreEqual(((MachineBlockConfigParam) data[3].Param).InputSlot, 2);
            Assert.AreEqual(((MachineBlockConfigParam) data[3].Param).OutputSlot, 3);

            Assert.AreEqual(data[10].BlockId, 10);
            Assert.AreEqual(data[10].Name, "TestMachine10");
            Assert.AreEqual(data[10].Type, VanillaBlockType.Machine);
            Assert.AreEqual(((MachineBlockConfigParam) data[10].Param).InputSlot, 5);
            Assert.AreEqual(((MachineBlockConfigParam) data[10].Param).OutputSlot, 1);

            Assert.AreEqual(data[11].BlockId, 11);
            Assert.AreEqual(data[11].Name, "TestMachine11");
            Assert.AreEqual(data[11].Type, VanillaBlockType.Machine);
            Assert.AreEqual(((MachineBlockConfigParam) data[11].Param).InputSlot, 2);
            Assert.AreEqual(((MachineBlockConfigParam) data[11].Param).OutputSlot, 6);
        }
    }
}