using System.Linq;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Components.Slots;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;
using S = Mooresmaster.Model.BlocksModule.BlockMasterElement.BlockScreenConst;

namespace Client.Tests.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 標準の本文は Web SectionStackView の区画の順と項目グリッドの出し分けを保つ（L1）
    /// The standard body keeps the Web SectionStackView section order and item-grid visibility (L1)
    /// </summary>
    public class SectionStackViewTest : BlockInventoryScreenTestHost
    {
        private const string ItemGridSectionName = "section-stack-item-grid";

        /// <web-test file="src/features/blockInventory/views/__tests__/SectionStackView.test.tsx">%s の既存表示を保つ</web-test>
        [TestCase("Chest", S.Chest, "chest-grid", true)]
        [TestCase("ElectricMachine", S.Machine, null, false)]
        [TestCase("GearMachine", S.Machine, null, false)]
        [TestCase("ElectricGenerator", S.ElectricGenerator, "generator-fuel-grid", false)]
        [TestCase("FuelGearGenerator", S.FuelGearGenerator, "generator-fuel-grid", true)]
        [TestCase("SimpleGearGenerator", S.FuelGearGenerator, "generator-fuel-grid", true)]
        [TestCase("ElectricMiner", S.ElectricMiner, "miner-output-grid", true)]
        [TestCase("GearMiner", S.GearMiner, "gear-miner-output-grid", true)]
        [TestCase("CleanRoomMachine", S.Machine, null, false)]
        [TestCase("CleanRoomAirFilter", S.CleanRoomAirFilter, "clean-room-filter-grid", true)]
        [TestCase("unknown", S.Generic, "generic-block-grid", false)]
        public void 項目グリッドは設定どおり空でも出すか枠があるときだけ出す(string blockType, string blockScreen, string gridId, bool renderEmptyGrid)
        {
            var source = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.OfType(blockType, blockScreen)));
            var body = BlockInventoryTestBodies.SectionStack(source, blockScreen);
            Mount(body.Root);
            body.Attach((OpenBlockInventoryPanel)source.Current.Value);
            simulate.FrameUpdate();
            Assert.AreEqual(gridId != null && renderEmptyGrid, IsShown(body.Root, ItemGridSectionName));

            source.State.Value = BlockInventoryTestPanels.OfType(blockType, blockScreen, new SlotItem(new ItemId(1), 1));
            simulate.FrameUpdate();
            Assert.AreEqual(gridId != null, IsShown(body.Root, ItemGridSectionName));
            if (gridId != null) Assert.AreEqual(gridId, body.Root.Q(ItemGridSectionName).Q<SlotGrid>().name);
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/SectionStackView.test.tsx">親から状態・供給・歯車・電力網・歯車網を順に描画する</web-test>
        [Test]
        public void 区画は固定の順に並ぶ()
        {
            var source = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.OfType("GearToElectricGenerator", S.GearToElectricGenerator)));
            var body = BlockInventoryTestBodies.SectionStack(source, S.GearToElectricGenerator, FakeSectionFactory.AllSlots(true));
            Mount(body.Root);
            body.Attach((OpenBlockInventoryPanel)source.Current.Value);
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { "fake-Context", "fake-Machine", "fake-Miner", "fake-Pump", "fake-Generator", "fake-GearToElectric", "fake-BeltThroughput", "fake-Gear", "fake-ElectricNetwork", "fake-GearNetwork", "fake-FluidDetails", "fake-FluidRow" }, ShownNames(body.Root));
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/SectionStackView.test.tsx">部屋欄はセクション列の先頭に描く</web-test>
        [Test]
        public void 文脈の区画は項目グリッドより上に来る()
        {
            var source = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.OfType("CleanRoomAirFilter", S.CleanRoomAirFilter, new SlotItem(new ItemId(1), 5))));
            var body = BlockInventoryTestBodies.SectionStack(source, S.CleanRoomAirFilter, new FakeSectionFactory(SectionStackSlot.Context, true));
            Mount(body.Root);
            body.Attach((OpenBlockInventoryPanel)source.Current.Value);
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { "fake-Context", ItemGridSectionName }, ShownNames(body.Root));
            Assert.AreEqual("clean-room-filter-grid", body.Root.Q(ItemGridSectionName).Q<SlotGrid>().name);
        }

        [Test]
        public void 区画の出し入れはパネルの描き直し無しで変わり先頭の間隔が詰まる()
        {
            // 網の集約・ベルトの統計のようにパネルの組み直しの外で届く変化でも、区画の通知だけで出し入れと間隔が組み直される
            // Even for changes arriving outside panel rebuilds (network totals, belt stats), the section's own signal re-lays visibility and gaps
            var source = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.OfType("GearToElectricGenerator", S.GearToElectricGenerator)));
            var context = new FakeSectionFactory(SectionStackSlot.Context, false);
            var machine = new FakeSectionFactory(SectionStackSlot.Machine, true);
            var body = BlockInventoryTestBodies.SectionStack(source, S.GearToElectricGenerator, context, machine);
            Mount(body.Root);
            body.Attach((OpenBlockInventoryPanel)source.Current.Value);
            simulate.FrameUpdate();
            Assert.AreEqual(0f, machine.Created.Root.resolvedStyle.marginTop);

            context.Created.Applicable.Value = true;
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { "fake-Context", "fake-Machine" }, ShownNames(body.Root));
            Assert.AreEqual(0f, context.Created.Root.resolvedStyle.marginTop);
            Assert.Less(0f, machine.Created.Root.resolvedStyle.marginTop);   // UiGap.Sm の実値（UiStack の USS）
        }

        private static bool IsShown(VisualElement root, string name)
        {
            return root.Q(name) is { } section && section.resolvedStyle.display == DisplayStyle.Flex;
        }

        private static string[] ShownNames(VisualElement root)
        {
            return root.Children().Where(e => e.resolvedStyle.display == DisplayStyle.Flex).Select(e => e.name).ToArray();
        }
    }
}
