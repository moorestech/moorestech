using Client.Tests.Ui.TestHost;
using System.IO;
using System.Text.RegularExpressions;
using Client.Tests.Ui.Components;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.Components.Gauges;
using Client.Ui.Components.Recipe;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory;
using Client.Ui.Screens.BlockInventory.Machine;
using Client.Ui.Screens.BlockInventory.Machine.RecipeSelection;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.BlockInventory.Panel;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;
using Fixtures = Client.Tests.Ui.Screens.BlockInventory.Machine.RecipeSelection.MachineRecipeSelectionFixtures;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 機械の区画の見た目の決まり
    /// Design rules of the machine section
    /// </summary>
    public class MachineDesignTest : MachineScreenTestHost
    {
        private const string ScreensUssRoot = "Scripts/Client.Ui/Screens";

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">%sから禁止Mantine部品とテーマ色を除去する</web-test>
        [TestCase("BlockInventory/Machine/Machine.uss")]
        [TestCase("BlockInventory/Family/Family.uss")]
        [TestCase("BlockInventory/Fluid/Fluid.uss")]
        [TestCase("BlockInventory/Machine/RecipeSelection/MachineRecipeSelection.uss")]
        public void 本レーンのUSSは色を直書きせずトークンだけを使う(string relativePath)
        {
            // alpha の色の件と同じ判定
            // Same as alpha's BlockInventoryDesignTest colour case
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
            // 偽 Model で行 0→1→0 と流す
            // Feed a fake machine model 0 rows, then 1, then 0
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

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">MachineSectionはレシピ選択リストとヘッダを往復し、電力率を共通フッタに置く</web-test>
        [Test]
        public void 選択リストとヘッダを往復し電力率は両モードで同じフッタに置く()
        {
            var recipe = Fixtures.ItemRecipe(MachineTestDetails.RecipeA, 5, SecondItem, FirstItem);
            var root = MountSection(Fixtures.Machine(MachineRecipeSelection.Selected(MachineTestDetails.RecipeA), false, recipe), true).Root;
            var footer = Find<VisualElement>(root, "machine-power-rate").GetFirstAncestorOfType<MachineSectionFooter>();
            Assert.IsNotNull(footer);
            Assert.IsNull(root.Q(className: "mode-switch"));

            simulate.Click(Find<VisualElement>(root, "machine-selected-recipe").worldBound.center);
            simulate.FrameUpdate();
            Assert.IsNotNull(root.Q(MachineRecipeSelectionList.ListTestid));
            Assert.AreSame(footer, Find<VisualElement>(root, "machine-power-rate").GetFirstAncestorOfType<MachineSectionFooter>());
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">レシピ選択行は共有RecipeRowを流用し、静止矢印で列挙する</web-test>
        [Test]
        public void 選択行は共有RecipeRowを使い矢印を静止させる()
        {
            var recipes = new[] { Fixtures.ItemRecipe(MachineTestDetails.RecipeA, 5, SecondItem, FirstItem), Fixtures.ItemRecipe(MachineTestDetails.RecipeB, 7, FirstItem, SecondItem) };
            var root = MountSection(Fixtures.Machine(MachineRecipeSelection.Unselected, false, recipes), true).Root;
            foreach (var guid in new[] { MachineTestDetails.RecipeA, MachineTestDetails.RecipeB })
            {
                var row = Find<VisualElement>(root, $"machine-recipe-{guid}");
                Assert.IsNotNull(row.Q<RecipeRow>($"machine-recipe-{guid}-row"));
                Assert.IsTrue(Find<ProgressArrowGlyph>(row, $"machine-recipe-{guid}-arrow").ClassListContains("progress-arrow-glyph--idle"));
            }
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">機械の加工行は進捗矢印を中央に固定する3カラムグリッドにする</web-test>
        [Test]
        public void 加工行は左右等幅の伸縮列で矢印を中央に固定する()
        {
            // 入出力の数が違っても矢印は中央
            // The arrow stays centred whatever the counts
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
            Assert.AreEqual(new Client.Ui.State.Text.UiText(Mooresmaster.Localization.Generated.LocalizationKeys.Ui.BlockInventory.UpgradeSlots).Resolve(), label.PlainText());

            // Web は xs×2 の間隔
            // On Web gap=xs plus mt=xs gives xs×2 from row bottom to label top
            var xs = ThemeTokenFile.ResolvePixels("--mantine-spacing-xs");
            var row = Find<VisualElement>(root, "machine-process-row");
            Assert.AreEqual(xs * 2, label.worldBound.yMin - row.worldBound.yMax, 0.5f);
        }
    }
}
