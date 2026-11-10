using System.IO;
using System.Text.RegularExpressions;
using Client.Tests.Ui.Components;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.BlockInventory.Panel;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 機械の区画の見た目の決まり（加工行の 3 列・モジュール帯の 1 段下げ）
    /// Design rules of the machine section (three-column process row, module band one level down)
    /// </summary>
    public class MachineDesignTest : MachineScreenTestHost
    {
        private const string ScreensUssRoot = "Scripts/Client.Ui/Screens";

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">%sから禁止Mantine部品とテーマ色を除去する</web-test>
        [TestCase("BlockInventory/Machine/Machine.uss")]
        [TestCase("BlockInventory/Family/Family.uss")]
        [TestCase("BlockInventory/Fluid/Fluid.uss")]
        public void 本レーンのUSSは色を直書きせずトークンだけを使う(string relativePath)
        {
            // alpha の BlockInventoryDesignTest の色の件と同じ判定（選択リストの USS は Task 12 で足す）
            // Same check as alpha's BlockInventoryDesignTest colour case (the selection-list USS is added in Task 12)
            var uss = File.ReadAllText(Path.Combine(Application.dataPath, ScreensUssRoot, relativePath));
            StringAssert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b|rgba?\(", uss);
            foreach (Match match in Regex.Matches(uss, @"^\s*(?:-unity-)?[a-z-]*color\s*:\s*([^;]+);", RegexOptions.Multiline))
            {
                StringAssert.StartsWith("var(--", match.Groups[1].Value.Trim(), match.Value);
            }
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">レシピ有り機械だけviewer〜items占有の大型パネルへ広げる</web-test>
        [Test]
        public void レシピの行がある機械だけ外殻が大型になる()
        {
            // 本物の寄与に偽の機械の Model を差し、行 0 → 行 1 → 行 0 と流す
            // Plug the real rule with a fake machine model and feed 0 rows, then 1, then 0
            var rule = new RecipeMachineLargePanelRule(MachineModel);
            rule.Initialize();
            var state = new ReactiveProperty<BlockInventoryPanelState>(MachineTestDetails.Panel(MachineTestDetails.DefaultInstanceId, new Client.Ui.State.Items.SlotItem[2]));
            var shell = BlockInventoryTestShell.Create(this, state, rule);
            simulate.FrameUpdate();
            Assert.IsFalse(shell.View.ClassListContains(BlockInventoryView.LargeClassName));

            MachineModel.Set(SectionContent<MachineSectionViewModel>.Present(MachineTestDetails.ViewModel(MachineRecipeSelection.Unselected)));
            simulate.FrameUpdate();
            Assert.IsFalse(shell.View.ClassListContains(BlockInventoryView.LargeClassName));

            MachineModel.Set(SectionContent<MachineSectionViewModel>.Present(MachineTestDetails.ViewModel(MachineRecipeSelection.Unselected, MachineTestDetails.RecipeA)));
            simulate.FrameUpdate();
            Assert.IsTrue(shell.View.ClassListContains(BlockInventoryView.LargeClassName));

            MachineModel.Set(SectionContent<MachineSectionViewModel>.Present(MachineTestDetails.ViewModel(MachineRecipeSelection.Unselected)));
            simulate.FrameUpdate();
            Assert.IsFalse(shell.View.ClassListContains(BlockInventoryView.LargeClassName));
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">機械の加工行は進捗矢印を中央に固定する3カラムグリッドにする</web-test>
        [Test]
        public void 加工行は左右等幅の伸縮列で矢印を中央に固定する()
        {
            // 入力 2・出力 1 と数を変えても矢印は行の中央
            // The arrow stays at the row centre even with 2 inputs and 1 output
            var root = MountSection(MachineScreenViewModels.Filled(2, 0, 0.5f, FirstItem, 1, 1), false).Root;
            var row = Find<VisualElement>(root, "machine-process-row");
            var input = row.Q(className: "machine-process-row__input");
            var output = row.Q(className: "machine-process-row__output");

            foreach (var side in new[] { input, output })
            {
                Assert.AreEqual(1f, side.resolvedStyle.flexGrow);
                Assert.AreEqual(0f, side.resolvedStyle.flexBasis.value);
            }
            var arrow = row.Q<ProgressArrowGlyph>("machine-progress-arrow");
            Assert.AreEqual(row.worldBound.center.x, arrow.worldBound.center.x, 0.5f);
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">モジュールスロットをラベル付きで加工行から1段下げる</web-test>
        [Test]
        public void モジュール帯はラベル付きで加工行から1段下げる()
        {
            var root = MountSection(MachineScreenViewModels.Filled(1, 2, 0, FirstItem, 1, 4), false).Root;
            var label = Find<Label>(root, "machine-module-label");
            Assert.IsTrue(label.ClassListContains(UiTypographyClass.TextSm));
            Assert.IsTrue(label.ClassListContains(UiTypographyClass.TextDimmed));
            Assert.AreEqual(new Client.Ui.State.Text.UiText(Mooresmaster.Localization.Generated.LocalizationKeys.Ui.BlockInventory.UpgradeSlots).Resolve(), label.text);

            // Web は親 Stack gap=xs＋帯の mt=xs。加工行の下端からラベルの上端まで xs×2
            // On the Web: parent Stack gap=xs plus the band's mt=xs, so xs×2 from the process row bottom to the label top
            var xs = ThemeTokenFile.ResolvePixels("--mantine-spacing-xs");
            var row = Find<VisualElement>(root, "machine-process-row");
            Assert.AreEqual(xs * 2, label.worldBound.yMin - row.worldBound.yMax, 0.5f);
        }
    }
}
