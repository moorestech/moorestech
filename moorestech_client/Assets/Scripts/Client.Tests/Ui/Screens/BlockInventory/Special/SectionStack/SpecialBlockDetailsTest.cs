using System.Linq;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.BlockInventory.Special.Details;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.SectionStack
{
    /// <summary>
    /// ブロック詳細の歯車とフィルタの面（Web blockDetails.spec・regression/sectionStack.spec。S4-machine が同じタグで機械・採掘機・ポンプ・発電機の面を写す）
    /// Gear and filter facets of the block details (Web blockDetails.spec, regression/sectionStack.spec; S4-machine ports the machine, miner, pump and generator facets under the same tags)
    /// </summary>
    public class SpecialBlockDetailsTest : SpecialScreenTestHost
    {
        private static readonly string[] SpecialSectionIds = { "gear-to-electric-section", "belt-throughput-section", "gear-section", "electric-network-section", "gear-network-section" };

        private static readonly string[] WebSectionIds =
        {
            "machine-section", "miner-section", "generator-section", "gear-section", "electric-network-section", "gear-network-section",
            "chest-grid", "machine-fluid-slots", "miner-output-grid", "gear-miner-output-grid", "generator-fuel-grid",
            "generic-block-grid", "generic-block-fluids", "filter-splitter", "pump-section", "pump-fluid-slots",
        };

        /// <web-test file="e2e/tests/block/blockDetails.spec.ts">renders ${type} detail section</web-test>
        [Test]
        public void 歯車機械の標準の本文に歯車の区画が出る()
        {
            var root = MountStack(BlockMasterElement.BlockTypeConst.GearMachine, SpecialDetailSamples.GearMachine);
            Assert.IsTrue(IsShown(Find<VisualElement>(root, "gear-section")));
        }

        /// <web-test file="e2e/tests/block/blockDetails.spec.ts">renders ${type} detail section</web-test>
        [Test]
        public void フィルタ分配器の専用の本文が出る()
        {
            var root = MountFilterSplitter();
            Assert.IsTrue(IsShown(Find<VisualElement>(root, "filter-splitter")));
        }

        /// <web-test file="e2e/tests/block/blockDetails.spec.ts">歯車機械は消費トルクと基準付きRPMを赤なしで出し、網の需給行を持つ</web-test>
        [Test]
        public void 歯車機械は消費トルクと基準付きRPMを不足色なしで出し歯車網の区画を持つ()
        {
            var root = MountStack(BlockMasterElement.BlockTypeConst.GearMachine, SpecialDetailSamples.GearMachine);
            var rows = GearRowDisplay.From(SpecialDetailSamples.GearMachineGear);
            Assert.AreEqual(Resolved(rows.Torque), TextOf(root, "gear-torque"));
            Assert.AreEqual(Resolved(rows.Rpm), TextOf(root, "gear-rpm"));
            StringAssert.Contains("3.0", TextOf(root, "gear-torque"));
            StringAssert.Contains("12.5 / 20.0", TextOf(root, "gear-rpm"));
            AssertNotInsufficient(root, "gear-torque", "gear-rpm");
            Assert.IsTrue(IsShown(Find<VisualElement>(root, "gear-network-section")));
        }

        /// <web-test file="e2e/tests/block/blockDetails.spec.ts">歯車発電機は発生トルクと現在RPMだけを出す</web-test>
        [Test]
        public void 歯車発電機は発生トルクと現在RPMだけを不足色なしで出す()
        {
            var root = MountStack(BlockMasterElement.BlockTypeConst.SimpleGearGenerator, SpecialDetailSamples.GearGenerator);
            StringAssert.Contains("5.0", TextOf(root, "gear-torque"));
            StringAssert.Contains("20.0", TextOf(root, "gear-rpm"));
            StringAssert.DoesNotContain("/", TextOf(root, "gear-rpm"));
            AssertNotInsufficient(root, "gear-torque", "gear-rpm");
        }

        /// <web-test file="e2e/tests/block/blockDetails.spec.ts">歯車ポンプは電力行を持たず歯車行を出す</web-test>
        [Test]
        public void 歯車ポンプは歯車の区画を出し電力網の区画を出さない()
        {
            var root = MountStack(BlockMasterElement.BlockTypeConst.GearPump, SpecialDetailSamples.GearMachine);
            Assert.IsTrue(IsShown(Find<VisualElement>(root, "gear-section")));
        }

        /// <web-test file="e2e/tests/regression/sectionStack.spec.ts">${entry.type}は対応セクションだけを表示する</web-test>
        [TestCase("GearMachine", true)]
        [TestCase("GearMiner", true)]
        [TestCase("GearPump", true)]
        [TestCase("SimpleGearGenerator", true)]
        [TestCase("ElectricMachine", false)]
        [TestCase("ElectricMiner", false)]
        [TestCase("ElectricGenerator", false)]
        [TestCase("ElectricPump", false)]
        public void 歯車駆動は歯車と歯車網の区画だけ電力駆動は電力網の区画だけを特殊族の区画として出す(string blockType, bool gearDriven)
        {
            // Web の cases の shown のうち特殊族の区画の id だけを見る（他の id は alpha・S4-machine が同じタグで見る）
            // Check only the special section ids among the Web cases' shown list (alpha and S4-machine check the rest under the same tag)
            var root = MountStack(blockType, gearDriven ? SpecialDetailSamples.GearMachine : SpecialDetailSamples.Only(SpecialDetailSamples.PoleNetwork));
            var expected = gearDriven ? new[] { "gear-section", "gear-network-section" } : new[] { "electric-network-section" };
            var shownSpecial = SpecialSectionIds.Where(id => root.Q(id) is { } section && IsShown(section)).ToArray();
            CollectionAssert.AreEqual(expected, shownSpecial);
        }

        /// <web-test file="e2e/tests/regression/sectionStack.spec.ts">${entry.type}は対応セクションだけを表示する</web-test>
        [Test]
        public void フィルタ分配器は専用の本文だけを出し他の区画を持たない()
        {
            // 種別の表で専用の本文に振られ、標準の本文の区画を一つも作らない
            // The registry routes it to the dedicated body, which creates none of the standard sections
            Assert.AreEqual(BlockInventoryBodyKind.FilterSplitter, BlockComponentRegistry.Resolve(BlockMasterElement.BlockTypeConst.FilterSplitter).Body);
            var root = MountFilterSplitter();
            Assert.IsTrue(IsShown(Find<VisualElement>(root, "filter-splitter")));
            CollectionAssert.IsEmpty(WebSectionIds.Where(id => id != "filter-splitter" && root.Q(id) != null).ToArray());
        }

        private VisualElement MountStack(string blockType, SpecialBlockDetailViewModel detail)
        {
            Model.Set(detail);
            var panel = OpenPanel(blockType);
            var body = BlockInventoryTestBodies.SectionStack(Panels, blockType, SectionFactories());
            Mount(body.Root);
            body.Attach(panel);
            simulate.FrameUpdate();
            return body.Root;
        }

        private VisualElement MountFilterSplitter()
        {
            // 本物の本文の工場 5 つと区画の工場 5 つを alpha の本文のホストに登録し、パネル全体の根を返す（混入を見られるように）
            // Register the real 5 body and 5 section factories on alpha's body host and return the whole panel root, so a stray section would show up
            Model.Set(SpecialDetailSamples.Only(SpecialDetailSamples.FilterSplitter));
            var kit = Kit(LoadContent());
            var bodies = new IBlockInventoryBodyFactory[] { new FilterSplitterBodyFactory(kit, Presenters), new ElectricToGearBodyFactory(kit, Presenters), new ElectricPoleBodyFactory(kit, Presenters), new SpaceElevatorBodyFactory(kit, Presenters), new RocketLaunchPadBodyFactory(kit, Presenters) };
            var shell = BlockInventoryTestShell.Create(this, Panels.State, bodies.Cast<object>().Concat(SectionFactories()).ToArray());
            OpenPanel(BlockMasterElement.BlockTypeConst.FilterSplitter);
            simulate.FrameUpdate();
            return shell.View;
        }

        // Web は文字色 rgb(238,238,238)（--text-default）で赤なしを見る。L1 は不足のクラスが無いことで見て、色の実値は並べ比べ（Task 10）で見る
        // The Web checks the text color rgb(238,238,238); L1 checks the absence of the shortage class, leaving the actual color to the side-by-side (Task 10)
        private static void AssertNotInsufficient(VisualElement root, params string[] testids)
        {
            foreach (var testid in testids) Assert.IsFalse(Find<VisualElement>(root, testid).ClassListContains("text--insufficient"), $"{testid} は不足色でない");
        }
    }
}
