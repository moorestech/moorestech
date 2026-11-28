using System.Linq;
using Client.Tests.Ui.Components;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Components.Gauges;
using Client.Ui.Components.Slots;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.Screens.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 左右の帯が中身より狭くならない（Web の 1fr）
    /// Side bands never shrink below their content (Web 1fr)
    /// </summary>
    public class MachineProcessRowWidthTest : MachineScreenTestHost
    {
        private const float PixelSnap = 0.6f;

        [TestCase(2, 2, 0)]
        [TestCase(1, 1, 2)]
        [TestCase(3, 1, 0)]
        [TestCase(2, 2, 4)]
        public void 自然高の機械は左右の帯が重ならず枠が潰れずパネルの内側に収まる(int inputCount, int outputCount, int moduleCount)
        {
            var shell = OpenMachine(inputCount, outputCount, moduleCount, false);
            var row = shell.View.Q<VisualElement>("machine-process-row");
            var input = row.Q(className: "machine-process-row__input");
            var output = row.Q(className: "machine-process-row__output");
            var inputGrid = row.Q<SlotGrid>("machine-input-slots");
            var outputGrid = row.Q<SlotGrid>("machine-output-slots");
            var arrow = row.Q<ProgressArrowGlyph>("machine-progress-arrow");
            var panelBody = shell.Panel().contentContainer.worldBound;
            var measured = $"panel={shell.Panel().worldBound} body={panelBody} row={row.worldBound} input={input.worldBound} inputGrid={inputGrid.worldBound} arrow={arrow.worldBound} output={output.worldBound} outputGrid={outputGrid.worldBound}";

            // 帯は中身より狭くならず重ならない
            // Bands stay at least content-wide and never overlap
            Assert.GreaterOrEqual(input.layout.width + PixelSnap, inputGrid.layout.width, measured);
            Assert.GreaterOrEqual(output.layout.width + PixelSnap, outputGrid.layout.width, measured);
            Assert.LessOrEqual(inputGrid.worldBound.xMax, arrow.worldBound.xMin + PixelSnap, measured);
            Assert.GreaterOrEqual(outputGrid.worldBound.xMin + PixelSnap, arrow.worldBound.xMax, measured);
            Assert.LessOrEqual(outputGrid.worldBound.xMax, panelBody.xMax + PixelSnap, measured);

            // 左右等幅で矢印が中央
            // Equal side widths with the arrow centred
            Assert.AreEqual(input.layout.width, output.layout.width, PixelSnap, measured);
            Assert.AreEqual(row.worldBound.center.x, arrow.worldBound.center.x, PixelSnap, measured);

            // 枠は slot-size の正方形
            // Frames stay slot-size squares
            var slotSize = ThemeTokenFile.ResolvePixels("--slot-size");
            var moduleFrames = moduleCount == 0 ? Enumerable.Empty<SlotFrame>() : shell.View.Q("machine-module-slots").Query<SlotFrame>().ToList();
            foreach (var frame in row.Query<SlotFrame>().ToList().Concat(moduleFrames))
            {
                Assert.AreEqual(slotSize, frame.layout.width, PixelSnap, $"{frame.name} {frame.layout} / {Chain(frame)} / {measured}");
                Assert.AreEqual(slotSize, frame.layout.height, PixelSnap, $"{frame.name} {frame.layout} / {Chain(frame)} / {measured}");
            }
        }

        [Test]
        public void レシピの行がある大型の機械でも左右は等幅で矢印は中央()
        {
            var shell = OpenMachine(2, 2, 0, true);
            var row = shell.View.Q<VisualElement>("machine-process-row");
            Assert.IsNotNull(row, Chain(shell.View.Q("block-inventory-body")) + " / " + string.Join(",", shell.View.Query<VisualElement>().ToList().Select(e => e.name).Where(n => !string.IsNullOrEmpty(n)).Take(40)));
            var input = row.Q(className: "machine-process-row__input");
            var output = row.Q(className: "machine-process-row__output");
            var arrow = row.Q<ProgressArrowGlyph>("machine-progress-arrow");
            Assert.AreEqual(input.layout.width, output.layout.width, PixelSnap);
            Assert.AreEqual(row.worldBound.center.x, arrow.worldBound.center.x, PixelSnap);
        }

        [Test]
        public void チェスト指定の機械は通常画面とレシピ選択で実枠を一度ずつ出す()
        {
            var instance = MachineTestDetails.DefaultInstanceId;
            var slots = Enumerable.Repeat(new SlotItem(FirstItem, 1), 3).ToArray();
            var original = MachineTestDetails.Panel(instance, slots);
            var panel = BlockInventoryTestPanels.OpenBlock(original.BlockType, Mooresmaster.Model.BlocksModule.BlockMasterElement.BlockScreenConst.Chest, original.BlockGuid, original.BlockId, instance, original.Position, slots, BlockInventoryPanelError.None);
            var machine = new MachineSectionFactory(MachineModel, ContextModel, Commands, Content, RecipeSelect, OsCursor, ImprovementCommands);
            var rule = new RecipeMachineLargePanelRule(MachineModel);
            rule.Initialize();
            var shell = BlockInventoryTestShell.Create(this, new ReactiveProperty<BlockInventoryPanelState>(panel), rule, machine);

            UpdateMachine(MachineRecipeSelection.Selected(MachineTestDetails.RecipeA), 0.1f);
            Assert.IsEmpty(Shown(shell.View, "chest-grid-slot-0"));
            Assert.AreEqual(1, Shown(shell.View, "machine-process-row").Count);
            Assert.AreEqual(1, Shown(shell.View, "machine-module-slots").Count);

            // 選択一覧では機械の在庫が外れ、共通グリッドの実枠は残る
            // In recipe selection the machine inventory is unmounted and the common grid keeps the real slots
            UpdateMachine(MachineRecipeSelection.Unselected, 0.1f);
            Assert.IsEmpty(Shown(shell.View, "machine-process-row"));
            Assert.AreEqual(1, Shown(shell.View, "chest-grid-slot-0").Count);
            Assert.AreEqual(1, Shown(shell.View, "chest-grid-slot-2").Count);
            var originalSlot = shell.View.Q("chest-grid-slot-0");
            UpdateMachine(MachineRecipeSelection.Unselected, 0.9f);
            Assert.AreSame(originalSlot, shell.View.Q("chest-grid-slot-0"));

            #region Internal

            void UpdateMachine(MachineRecipeSelection selection, float progress)
            {
                var detail = MachineTestDetails.Detail(selection, MachineTestDetails.Unbound(new MachineSlotLayout(1, 1, 1)), progress);
                SetMachine(MachineTestDetails.ViewModelFor(instance, detail, slots.Length, MachineTestDetails.Catalog(MachineTestDetails.RecipeA)));
            }

            #endregion
        }

        // 失敗時に祖先の高さと縮み方を並べる
        // On failure list ancestors' heights and shrink settings
        private static string Chain(VisualElement element)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (var e = element; e != null && parts.Count < 18; e = e.hierarchy.parent)
            {
                parts.Add($"{e.name}[{string.Join(".", e.GetClasses())}] h={e.layout.height:0.#} shrink={e.resolvedStyle.flexShrink} basis={e.resolvedStyle.flexBasis} minH={e.resolvedStyle.minHeight} maxH={e.resolvedStyle.maxHeight}");
            }
            return string.Join(" < ", parts);
        }

        // 機械の区画を外殻に載せ、レシピで小型・大型を決める
        // Mount machine sections in the shell; recipes pick small or large
        private BlockInventoryTestShell OpenMachine(int inputCount, int outputCount, int moduleCount, bool withRecipe)
        {
            var layout = new MachineSlotLayout(inputCount, outputCount, moduleCount);
            // レシピがあれば選んだ状態にする（未選択だと選択の一覧が出て加工行が無い）
            // With recipes, select one (unselected shows the selection list without a process row)
            var selection = withRecipe ? MachineRecipeSelection.Selected(MachineTestDetails.RecipeA) : MachineRecipeSelection.Unselected;
            var detail = MachineTestDetails.Detail(selection, MachineTestDetails.Unbound(layout), 0.5f);
            var catalog = withRecipe ? MachineTestDetails.Catalog(MachineTestDetails.RecipeA) : MachineRecipeCatalog.Empty;
            var slotCount = inputCount + outputCount + moduleCount;
            MachineModel.Set(SectionContent<MachineSectionViewModel>.Present(MachineTestDetails.ViewModelFor(MachineTestDetails.DefaultInstanceId, detail, slotCount, catalog)));

            var rule = new RecipeMachineLargePanelRule(MachineModel);
            rule.Initialize();
            var panel = MachineTestDetails.Panel(MachineTestDetails.DefaultInstanceId, Enumerable.Repeat(new SlotItem(FirstItem, 1), slotCount).ToArray());
            var context = new ContextSectionFactory(Content, ContextModel, MachineModel, OsCursor, ImprovementCommands);
            var machine = new MachineSectionFactory(MachineModel, ContextModel, Commands, Content, RecipeSelect, OsCursor, ImprovementCommands);
            var shell = BlockInventoryTestShell.Create(this, new ReactiveProperty<BlockInventoryPanelState>(panel), rule, context, machine);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            return shell;
        }
    }
}
