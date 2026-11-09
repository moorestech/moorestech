using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Components.Slots;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 項目グリッドの id が blockType ごとに Web の e2e と同じ（区画の id は持ち主のレーンのテストが写す）
    /// Item-grid ids per blockType match the Web e2e (section ids are ported by the owning lanes' tests)
    /// </summary>
    public class SectionStackRegressionTest : BlockInventoryScreenTestHost
    {
        /// <web-test file="e2e/tests/regression/sectionStack.spec.ts">${entry.type}は対応セクションだけを表示する</web-test>
        [TestCase("Chest", "chest-grid")]
        [TestCase("GearMachine", null)]
        [TestCase("GearMiner", "gear-miner-output-grid")]
        [TestCase("ElectricGenerator", "generator-fuel-grid")]
        [TestCase("FuelGearGenerator", "generator-fuel-grid")]
        [TestCase("unknown", "generic-block-grid")]
        public void 項目グリッドは対応するblockTypeだけに出る(string blockType, string gridId)
        {
            var source = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.OfType(blockType, new SlotItem(new ItemId(1), 1))));
            var body = BlockInventoryTestBodies.SectionStack(source, blockType);
            Mount(body.Root);
            body.Attach((OpenBlockInventoryPanel)source.Current.Value);
            simulate.FrameUpdate();

            var section = body.Root.Q("section-stack-item-grid");
            Assert.AreEqual(gridId != null, section.resolvedStyle.display == DisplayStyle.Flex);
            if (gridId != null) Assert.AreEqual(gridId, section.Q<SlotGrid>().name);
        }
    }
}
