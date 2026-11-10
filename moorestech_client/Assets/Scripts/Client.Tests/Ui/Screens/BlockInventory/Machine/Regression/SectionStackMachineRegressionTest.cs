using System.Linq;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.State.BlockInventory.Context;
using Client.Tests.Ui.State.BlockInventory.Family;
using Client.Tests.Ui.State.BlockInventory.Machine.Selection;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.ModeHud;
using Client.Ui.Content;
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
    /// 本レーンの区画の id が entry ごとに Web の e2e と同じだけ出る（本番の 7 工場を偽の Model で標準の本文に差す。他レーンの id は持ち主のテストが見る）
    /// This lane's section ids per entry match the Web e2e (the 7 production factories with fake models in the standard body; other lanes' ids are covered by their owners)
    /// </summary>
    public class SectionStackMachineRegressionTest : BlockInventoryScreenTestHost
    {
        // Web の sectionIds のうち本レーンが持つ id（Web の並び順）
        // The ids of Web sectionIds owned by this lane (in Web order)
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

            // 本レーンの id のうち自分も祖先も隠されていないものを Web の並びで集める
            // Collect this lane's ids that are hidden neither themselves nor via an ancestor, in Web order
            var shown = OwnSectionIds.Where(id => root.Query<VisualElement>(id).ToList().Any(IsEffectivelyShown)).ToArray();
            CollectionAssert.AreEqual(expectedOwnIds, shown, string.Join(",", shown));
        }

        // 本番の 7 工場に entry の値を入れた偽の Model を差し、標準の本文を組んで載せる
        // Plug the 7 production factories with fake models holding the entry's values, then build and mount the standard body
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
                new MachineSectionFactory(machine, context, new RecordingBlockSlotCommands(), Toaster, content, new RecordingRecipeSelectCommand()),
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
