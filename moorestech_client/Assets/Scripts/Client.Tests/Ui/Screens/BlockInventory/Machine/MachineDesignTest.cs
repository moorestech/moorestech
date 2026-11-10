using Client.Tests.Ui.Components;
using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Text;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 機械の区画の見た目の決まり（加工行の 3 列・モジュール帯の 1 段下げ）
    /// Design rules of the machine section (three-column process row, module band one level down)
    /// </summary>
    public class MachineDesignTest : MachineScreenTestHost
    {
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
