using System.Linq;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.State.BlockInventory.Context;
using Client.Tests.Ui.State.BlockInventory.Family;
using Client.Tests.Ui.State.BlockInventory.Machine.Selection;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.ModeHud;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.Screens.BlockInventory.Family;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Machine;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Panel;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine.Regression
{
    /// <summary>
    /// 区画 id が Web の e2e と同じ
    /// Section ids per entry match the Web e2e; other lanes' ids are covered by their owners
    /// </summary>
    public class SectionStackMachineRegressionTest : BlockInventoryScreenTestHost
    {
        // 本レーンの Web sectionIds
        // The Web sectionIds owned by this lane, in Web order
        private static readonly string[] OwnSectionIds =
        {
            "machine-section", "miner-section", "generator-section", "machine-fluid-slots", "generic-block-fluids", "pump-section", "pump-fluid-slots",
        };

        private static readonly Texture2D Texture = new(4, 4) { hideFlags = HideFlags.DontSave };
        private PlacementNameTestDictionary _names;

        [SetUp]
        public void SetUpRegressionNames()
        {
            _names = new PlacementNameTestDictionary();
            _names.Merge($"{ContentLocalizationKeys.ItemName(SectionStackRegressionEntries.ItemGuid).Key},Test Item 1,Test Item 1,テスト品1\n{ContentLocalizationKeys.FluidName(SectionStackRegressionEntries.WaterGuid).Key},Test Water,Test Water,テスト水\n");
        }

        [TearDown]
        public void TearDownRegressionNames()
        {
            _names.Restore();
        }

        /// <web-test file="e2e/tests/regression/sectionStack.spec.ts">${entry.type}は対応セクションだけを表示する</web-test>
        [TestCase("chest", new string[0])]
        [TestCase("tank", new[] { "generic-block-fluids" })]
        [TestCase("machine", new[] { "machine-section", "machine-fluid-slots" })]
        [TestCase("gearMachine", new[] { "machine-section" })]
        [TestCase("miner", new[] { "miner-section" })]
        [TestCase("gearMiner", new[] { "miner-section" })]
        [TestCase("generator", new[] { "generator-section" })]
        [TestCase("generic", new[] { "generic-block-fluids" })]
        [TestCase("filterSplitter", new string[0])]
        [TestCase("pump", new[] { "pump-section", "pump-fluid-slots" })]
        [TestCase("gearPump", new[] { "pump-section", "pump-fluid-slots" })]
        [TestCase("gearGenerator", new string[0])]
        public void 対応する区画だけを出す(string entryType, string[] expectedOwnIds)
        {
            var entry = SectionStackRegressionEntries.For(entryType);
            var root = MountBody(entry);

            // 隠れていない id を Web の並びで
            // Visible lane ids in Web order
            var shown = OwnSectionIds.Where(id => root.Query<VisualElement>(id).ToList().Any(IsEffectivelyShown)).ToArray();
            CollectionAssert.AreEqual(expectedOwnIds, shown, string.Join(",", shown));
        }

        // 7 工場に偽の Model を差し本文を載せる
        // Plug fake models into 7 factories and mount the body
        private VisualElement MountBody(SectionStackRegressionEntry entry)
        {
            var machine = new FakeMachineSectionModel();
            var context = new FakeBlockContextModel();
            var miner = new FakeMinerDetailModel();
            var pump = new FakePumpDetailModel();
            var generator = new FakeGeneratorDetailModel();
            var fluids = new FakeBlockFluidSlotsModel();
            machine.Set(entry.Machine);
            miner.Set(entry.Miner);
            pump.Set(entry.Pump);
            generator.Set(entry.Generator);
            fluids.Set(entry.Fluids);

            var content = new GameContentPresentation(new FakeContentTextureSource(Texture, Texture, Texture));
            var factories = new ISectionStackSectionFactory[]
            {
                new ContextSectionFactory(content, context, machine),
                new MachineSectionFactory(machine, context, new RecordingBlockSlotCommands(), Toaster, content, new RecordingRecipeSelectCommand(), new UiOsCursorRequestStack(new FakeUiOsCursorApplier())),
                new MinerSectionFactory(content, miner),
                new PumpSectionFactory(content, pump),
                new GeneratorSectionFactory(generator),
                new FluidDetailsSectionFactory(content, fluids),
                new FluidRowSectionFactory(content, fluids, machine),
            };

            var panel = BlockInventoryTestPanels.OfType(entry.BlockType);
            var source = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(panel));
            var body = BlockInventoryTestBodies.SectionStack(source, entry.BlockType, factories);
            Mount(body.Root);
            body.Attach((OpenBlockInventoryPanel)panel);
            simulate.FrameUpdate();
            return body.Root;
        }

        private static bool IsEffectivelyShown(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None) return false;
            }
            return true;
        }
    }
}
