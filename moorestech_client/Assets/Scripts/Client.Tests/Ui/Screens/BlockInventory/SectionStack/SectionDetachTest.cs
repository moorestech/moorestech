using Client.Game.InGame.BlockInfoPolling;
using Client.Tests.Ui.Screens.BlockInventory.Machine;
using Client.Tests.Ui.State.BlockInventory.Family;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.Screens.BlockInventory.Family;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Machine;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Family;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SectionStack;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 区画の Presenter は Detach で IsApplicable を偽へ戻し、前のパネルの適用可否を次のパネルへ持ち越さない（AC-18）
    /// Section presenters reset IsApplicable to false on Detach so the previous panel's applicability is never carried over (AC-18)
    /// </summary>
    public class SectionDetachTest : MachineScreenTestHost
    {
        [Test]
        public void 文脈の区画はDetachで偽へ戻る()
        {
            ContextModel.Set(new BlockContextViewModel(ContextEntry<CleanRoomFetchedDetail>.Unavailable(BlockInfoFetchState.NotFetched), ContextEntry<CleanRoomYieldFetched>.Absent, ContextEntry<MachineInputStall>.Absent));
            AssertResetOnDetach(new ContextSectionFactory(Content, ContextModel, MachineModel).Create(Mount("Chest")), "Chest");
        }

        [Test]
        public void 機械の区画はDetachで偽へ戻る()
        {
            MachineModel.Set(SectionContent<MachineSectionViewModel>.Present(MachineTestDetails.ViewModelWithModules(0, 0f, 1)));
            AssertResetOnDetach(new MachineSectionFactory(MachineModel, ContextModel, Commands, Toaster, Content, RecipeSelect, OsCursor).Create(Mount("ElectricMachine")), "ElectricMachine");
        }

        [Test]
        public void 族の詳細の区画はDetachで偽へ戻る()
        {
            var generator = new FakeGeneratorDetailModel();
            generator.Set(SectionContent<GeneratorDetail>.Present(new GeneratorDetail(10, 20, 0.5)));
            AssertResetOnDetach(new GeneratorSectionFactory(generator).Create(Mount("ElectricGenerator")), "ElectricGenerator");
        }

        [TestCase("unknown")]
        [TestCase("FluidPipe")]
        public void 液体の区画はDetachで偽へ戻る(string blockType)
        {
            var fluids = new FakeBlockFluidSlotsModel();
            fluids.Set(new BlockFluidSlotsViewModel(new[] { BlockFluidSlotState.Empty(100) }, false, 0f));
            var mount = Mount(blockType);
            ISectionStackSectionFactory factory = mount.Config.Fluid is RowSectionStackFluid ? new FluidRowSectionFactory(Content, fluids, MachineModel) : new FluidDetailsSectionFactory(Content, fluids);
            AssertResetOnDetach(factory.Create(mount), blockType);
        }

        [Test]
        public void 項目グリッドはDetachと別パネルの値で偽へ戻る()
        {
            var opened = BlockInventoryTestPanels.ChestWithSlots(new SlotItem(new ItemId(1), 3));
            var panels = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(opened));
            var section = new SectionStackItemGridSection(SectionStackConfigTable.Resolve("Chest").ItemGrid, BlockInventoryTestBodies.Grids(), panels);

            section.Attach((OpenBlockInventoryPanel)opened);
            Assert.IsTrue(section.IsApplicable.Value);
            panels.State.Value = BlockInventoryTestPanels.ChestAtPosition(new Vector3Int(5, 0, 0), new SlotItem(new ItemId(1), 3));
            Assert.IsFalse(section.IsApplicable.Value, "別のパネルへ替わる途中の値では偽へ倒す");

            panels.State.Value = opened;
            Assert.IsTrue(section.IsApplicable.Value);
            section.Detach();
            Assert.IsFalse(section.IsApplicable.Value, "Detach で偽へ戻る");
        }

        private static SectionStackSectionMount Mount(string blockType)
        {
            return new SectionStackSectionMount(SectionStackConfigTable.Resolve(blockType), false);
        }

        // 値を先に入れて Attach すると真、Detach で偽
        // True after Attach with the value already set, false after Detach
        private static void AssertResetOnDetach(IBlockInventorySection section, string blockType)
        {
            section.Attach((OpenBlockInventoryPanel)BlockInventoryTestPanels.OfType(blockType));
            Assert.IsTrue(section.IsApplicable.Value, "Attach 時点の値で出す側にいる");
            section.Detach();
            Assert.IsFalse(section.IsApplicable.Value, "Detach で偽へ戻る");
        }
    }
}
