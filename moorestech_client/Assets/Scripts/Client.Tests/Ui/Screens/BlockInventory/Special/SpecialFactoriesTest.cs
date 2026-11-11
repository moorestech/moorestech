using System.Linq;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.Foundation.Layout;
using Client.Ui.State.Items;
using Core.Master;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// 工場の Kind・Slot が持ち分どおりで重ならず、作った部分の根が Web の testid を持つ
    /// Factory kinds and slots match this lane's share without overlap, and each created part's root carries the Web testid
    /// </summary>
    public class SpecialFactoriesTest : SpecialScreenTestHost
    {
        [Test]
        public void 本文5つと題の操作1つと区画5つを持ち分どおりに作る()
        {
            var kit = Kit(null);
            var bodies = new IBlockInventoryBodyFactory[] { new FilterSplitterBodyFactory(kit, Presenters), new ElectricToGearBodyFactory(kit, Presenters), new ElectricPoleBodyFactory(kit, Presenters), new SpaceElevatorBodyFactory(kit, Presenters), new RocketLaunchPadBodyFactory(kit, Presenters) };
            CollectionAssert.AreEquivalent(new[] { BlockInventoryBodyKind.FilterSplitter, BlockInventoryBodyKind.ElectricToGear, BlockInventoryBodyKind.ElectricPole, BlockInventoryBodyKind.SpaceElevator, BlockInventoryBodyKind.RocketLaunchPad }, bodies.Select(b => b.Kind));
            CollectionAssert.AreEquivalent(new[] { "filter-splitter", "electric-to-gear-view", "electric-pole-view", "space-elevator-view", string.Empty }, bodies.Select(b => b.Create(new BlockInventoryBodyMount(false)).Root.name));

            Assert.AreEqual(BlockInventoryTitleActionKind.RocketLaunchPadCommand, new RocketLaunchPadTitleActionFactory(kit, Presenters).Kind);

            var sections = SectionFactories();
            CollectionAssert.AreEquivalent(new[] { SectionStackSlot.GearToElectric, SectionStackSlot.BeltThroughput, SectionStackSlot.Gear, SectionStackSlot.ElectricNetwork, SectionStackSlot.GearNetwork }, sections.Select(s => s.Slot));
            CollectionAssert.AreEquivalent(new[] { "gear-to-electric-section", "belt-throughput-section", "gear-section", "electric-network-section", "gear-network-section" }, sections.Select(s => s.Create(new SectionStackSectionMount(null, false)).Root.name));
        }

        [Test]
        public void 発射台の題の操作は題の操作の器の唯一の子で間隔の器の項目ではない()
        {
            // 題の操作の根は自分の display で隠れる前提なので、alpha の器へ差したときに UiGapContainer の項目でないことを実物で見る
            // The title action root hides itself, so check on the real shell that alpha's holder does not make it a UiGapContainer item
            Model.Set(SpecialDetailSamples.Only(SpecialDetailSamples.Rocket(RocketLaunchPadPhase.AwaitingBuildCommand)));
            var kit = Kit(LoadContent());
            var shell = BlockInventoryTestShell.Create(this, Panels.State, new RocketLaunchPadBodyFactory(kit, Presenters), new RocketLaunchPadTitleActionFactory(kit, Presenters));
            OpenPanel(BlockMasterElement.BlockTypeConst.RocketLaunchPad, new SlotItem(ItemMaster.EmptyItemId, 0), new SlotItem(ItemMaster.EmptyItemId, 0), new SlotItem(ItemMaster.EmptyItemId, 0));
            simulate.FrameUpdate();

            var action = Find<VisualElement>(shell.View, "rocket-launch-pad-title-action");
            Assert.IsTrue(IsShown(action), "建造の合図待ちで題の操作が出る");
            Assert.AreEqual("block-inventory-title-action", action.parent.name);
            Assert.AreEqual(1, action.parent.childCount, "題の操作の器の子は題の操作だけ");
            Assert.IsNotInstanceOf<UiGapContainer>(action.parent, "器は間隔の器でない");
            Assert.IsNotInstanceOf<UiGapContainer>(action.parent.parent, "器を載せる GamePanel の題の操作の欄も間隔の器でない");
        }
    }
}
