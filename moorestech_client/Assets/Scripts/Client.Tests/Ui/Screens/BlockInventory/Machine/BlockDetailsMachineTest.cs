using Client.Tests.Ui.State.BlockInventory.Family;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Screens.BlockInventory.Family;
using Client.Ui.Screens.BlockInventory.Machine;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Family;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Panel;
using Client.Tests.Ui.State.BlockInventory.Machine.Selection;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 標準の本文の中の機械の区画（出し入れ・歯車駆動の充足率・分間生産数）。本物の工場と偽の Model で組む
    /// The machine section inside the standard body (visibility, gear-driven power rate, per-minute production) on real factories and fake models
    /// </summary>
    public class BlockDetailsMachineTest : MachineScreenTestHost
    {
        private const string InsufficientClass = LackHighlightText.ClassName + "--insufficient";
        private FakeMinerDetailModel _miner;

        private IBlockInventoryPart MountBody()
        {
            _miner = new FakeMinerDetailModel();
            var panel = BlockInventoryTestPanels.OfType("ElectricMachine");
            var source = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(panel));
            var factories = new ISectionStackSectionFactory[]
            {
                new MachineSectionFactory(MachineModel, ContextModel, Commands, Toaster, Content, new RecordingRecipeSelectCommand()),
                new MinerSectionFactory(Content, _miner),
            };
            var body = BlockInventoryTestBodies.SectionStack(source, "ElectricMachine", factories);
            Mount(body.Root);
            body.Attach((OpenBlockInventoryPanel)panel);
            simulate.FrameUpdate();
            return body;
        }

        /// <web-test file="e2e/tests/block/blockDetails.spec.ts">renders ${type} detail section</web-test>
        [Test]
        public void 機械は機械の区画を出す()
        {
            var body = MountBody();
            Assert.AreEqual(0, Shown(body.Root, "machine-section").Count);

            SetMachine(MachineScreenViewModels.WithState(MachineProcessDisplayState.Processing, 1, 1, false, false));
            Assert.AreEqual(1, Shown(body.Root, "machine-section").Count);
        }

        /// <web-test file="e2e/tests/block/blockDetails.spec.ts">${type} は充足率不足でも充足率行を赤にしない</web-test>
        [TestCase("gearMachine")]
        [TestCase("gearMiner")]
        public void 歯車駆動は充足率不足でも充足率行を赤にしない(string type)
        {
            var body = MountBody();
            string rateTestid;
            if (type == "gearMachine")
            {
                SetMachine(MachineScreenViewModels.WithState(MachineProcessDisplayState.Processing, 50, 100, true, false));
                rateTestid = "machine-power-rate";
            }
            else
            {
                _miner.Set(SectionContent<MinerDetail>.Present(new MinerDetail(50, 100, 0, true, new MinerMiningItem[0])));
                simulate.FrameUpdate();
                rateTestid = "miner-power-rate";
            }

            var rate = body.Root.Q<Label>(rateTestid);
            StringAssert.Contains("%", rate.text);
            Assert.IsFalse(rate.ClassListContains(InsufficientClass));
            Assert.IsNull(body.Root.Q("gear-stop-reason"));
        }

        /// <web-test file="e2e/tests/block/blockDetails.spec.ts">機械recipeの出力個数と秒数から分間生産数を表示する</web-test>
        [Test]
        public void 出力個数と秒数から分間生産数を出す()
        {
            var body = MountBody();
            SetMachine(MachineScreenViewModels.Running(4, new RecipeOutputItem(FirstItem, 2)));

            var rows = Shown(body.Root, "machine-items-per-minute");
            Assert.AreEqual(1, rows.Count);
            var labels = rows[0].Query<Label>().ToList();
            Assert.AreEqual("30", labels[labels.Count - 1].text);

            // 秒が 0 の出力は行を出さない
            // An output with zero seconds gets no row
            SetMachine(MachineScreenViewModels.Running(0, new RecipeOutputItem(FirstItem, 2)));
            Assert.AreEqual(0, Shown(body.Root, "machine-items-per-minute").Count);
        }
    }
}
