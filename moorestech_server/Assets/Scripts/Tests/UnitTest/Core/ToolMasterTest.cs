using System.Linq;
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
            Assert.AreEqual(2, MasterHolder.ToolMaster.All.Count);

            // tools記載のアイテムはIsTool=true、未記載はfalse
            // Listed items are tools; unlisted items are not
            var toolGuid = MasterHolder.ToolMaster.All[0].ToolItemGuid;
            var toolItemId = MasterHolder.ItemMaster.GetItemId(toolGuid);
            Assert.IsTrue(MasterHolder.ToolMaster.IsTool(toolItemId));

            var nonTool = MasterHolder.ItemMaster.GetItemAllIds().First(id => id != toolItemId && !MasterHolder.ToolMaster.All.Any(t => MasterHolder.ItemMaster.GetItemId(t.ToolItemGuid) == id));
            Assert.IsFalse(MasterHolder.ToolMaster.IsTool(nonTool));
        }
    }
}
