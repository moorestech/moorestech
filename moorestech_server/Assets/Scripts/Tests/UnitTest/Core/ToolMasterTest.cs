using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core
{
    public class ToolMasterTest
    {
        [Test]
        public void Toolsと装備スロット数をロードできる()
        {
            // DIコンテナ生成でMasterHolderがロードされる
            // Building the DI container loads MasterHolder
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            Assert.AreEqual(3, MasterHolder.ToolMaster.EquipmentSlotCount);
            Assert.AreEqual(1, MasterHolder.ToolMaster.All.Count);

            // tools記載のtoolItemGuidが実在アイテムとして解決できる
            // The listed toolItemGuid resolves to an existing item
            var toolGuid = MasterHolder.ToolMaster.All[0].ToolItemGuid;
            Assert.IsNotNull(MasterHolder.ItemMaster.GetItemIdOrNull(toolGuid));
        }
    }
}
