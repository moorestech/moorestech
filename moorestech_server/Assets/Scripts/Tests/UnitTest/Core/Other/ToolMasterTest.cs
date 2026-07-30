using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Other
{
    public class ToolMasterTest
    {
        [Test]
        public void 装備スロット数をロードできる()
        {
            // DIコンテナ生成でMasterHolderがロードされる
            // Building the DI container loads MasterHolder
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            Assert.AreEqual(3, MasterHolder.ToolMaster.EquipmentSlotCount);
        }
    }
}
