using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Game.InGame.BlockInfoPolling;
using Client.Tests.Ui.Screens.BlockInventory.Machine;
using Client.Tests.Ui.State.BlockInventory.Family;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.Screens.BlockInventory.Family;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Machine;
using Client.Ui.Screens.BlockInventory.Parts;
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
using UnityEngine.TestTools;
using S = Mooresmaster.Model.BlocksModule.BlockMasterElement.BlockScreenConst;

namespace Client.Tests.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 区画の Presenter は Detach で IsApplicable を偽へ戻し、前のパネルの適用可否を次のパネルへ持ち越さない（AC-18）。汎用 Presenter の縮退も見る
    /// Section presenters reset IsApplicable to false on Detach so the previous panel's applicability is never carried over (AC-18); also covers the generic presenter's fallback
    /// </summary>
    public class SectionDetachTest : MachineScreenTestHost
    {
        [Test]
        public void 文脈の区画はDetachで偽へ戻る()
        {
            ContextModel.Set(new BlockContextViewModel(SectionContent<CleanRoomFetchedDetail>.Unavailable(BlockInfoFetchState.NotFetched), SectionContent<CleanRoomYieldOdds>.Absent, SectionContent<MachineInputStall>.Absent));
            AssertResetOnDetach(new ContextSectionFactory(Content, ContextModel, MachineModel, OsCursor, ImprovementCommands).Create(Mount(S.Chest)), "Chest", S.Chest);
        }

        [Test]
        public void 機械の区画はDetachで偽へ戻る()
        {
            MachineModel.Set(SectionContent<MachineSectionViewModel>.Present(MachineTestDetails.ViewModelWithModules(0, 0f, 1)));
            AssertResetOnDetach(new MachineSectionFactory(MachineModel, ContextModel, Commands, Content, RecipeSelect, OsCursor, ImprovementCommands).Create(Mount(S.Machine)), "ElectricMachine", S.Machine);
        }

        [Test]
        public void 族の詳細の区画はDetachで偽へ戻る()
        {
            var generator = new FakeGeneratorDetailModel();
            generator.Set(SectionContent<GeneratorDetail>.Present(new GeneratorDetail(10, 20, 0.5)));
            AssertResetOnDetach(new GeneratorSectionFactory(generator).Create(Mount(S.ElectricGenerator)), "ElectricGenerator", S.ElectricGenerator);
        }

        [TestCase("unknown", S.Generic)]
        [TestCase("FluidPipe", S.FluidPipe)]
        public void 液体の区画はDetachで偽へ戻る(string blockType, string blockScreen)
        {
            var fluids = new FakeBlockFluidSlotsModel();
            fluids.Set(new BlockFluidSlotsViewModel(new[] { BlockFluidSlotState.Empty(100) }, false, 0f));
            var mount = Mount(blockScreen);
            ISectionStackSectionFactory factory = mount.Config.Fluid is RowSectionStackFluid ? new FluidRowSectionFactory(Content, fluids, MachineModel) : new FluidDetailsSectionFactory(Content, fluids);
            AssertResetOnDetach(factory.Create(mount), blockType, blockScreen);
        }

        [Test]
        public void 項目グリッドはDetachと別パネルの値で偽へ戻る()
        {
            var opened = BlockInventoryTestPanels.ChestWithSlots(new SlotItem(new ItemId(1), 3));
            var panels = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(opened));
            var section = new SectionStackItemGridSection(SectionStackConfigTable.Resolve(S.Chest).ItemGrid, BlockInventoryTestBodies.Grids(), panels, Observable.Return<IReadOnlyList<int>>(System.Array.Empty<int>()));

            section.Attach((OpenBlockInventoryPanel)opened);
            Assert.IsTrue(section.IsApplicable.Value);
            panels.State.Value = BlockInventoryTestPanels.ChestAtPosition(new Vector3Int(5, 0, 0), new SlotItem(new ItemId(1), 3));
            Assert.IsFalse(section.IsApplicable.Value, "別のパネルへ替わる途中の値では偽へ倒す");

            panels.State.Value = opened;
            Assert.IsTrue(section.IsApplicable.Value);
            section.Detach();
            Assert.IsFalse(section.IsApplicable.Value, "Detach で偽へ戻る");
        }

        [Test]
        public void 汎用Presenterは取得できないの値を隠して開発者へ知らせる()
        {
            var generator = new FakeGeneratorDetailModel();
            generator.Set(SectionContent<GeneratorDetail>.Unavailable(BlockInfoFetchState.NotFetched));
            var section = new DetailSectionPresenter<GeneratorDetail>(generator.Current, new GeneratorSectionView());

            LogAssert.Expect(LogType.Error, new Regex(@"\[DetailSectionPresenter\] GeneratorDetail: this section has no unavailable line"));
            section.Attach((OpenBlockInventoryPanel)BlockInventoryTestPanels.OfType("ElectricGenerator", S.ElectricGenerator));
            Assert.IsFalse(section.IsApplicable.Value, "「取得できません」の行を持たない区画は隠す");
        }

        private static SectionStackSectionMount Mount(string blockScreen)
        {
            return new SectionStackSectionMount(SectionStackConfigTable.Resolve(blockScreen), false);
        }

        // 値を先に入れて Attach すると真、Detach で偽
        // True after Attach with the value already set, false after Detach
        private static void AssertResetOnDetach(IBlockInventorySection section, string blockType, string blockScreen)
        {
            section.Attach((OpenBlockInventoryPanel)BlockInventoryTestPanels.OfType(blockType, blockScreen));
            Assert.IsTrue(section.IsApplicable.Value, "Attach 時点の値で出す側にいる");
            section.Detach();
            Assert.IsFalse(section.IsApplicable.Value, "Detach で偽へ戻る");
        }
    }
}
