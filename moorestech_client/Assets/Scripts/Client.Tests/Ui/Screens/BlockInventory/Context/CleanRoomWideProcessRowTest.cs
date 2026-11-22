using Client.Tests.Ui.Components;
using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Screens.BlockInventory;
using Client.Ui.State.BlockInventory.Logic.Detail;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 出力の多い室内機械でも舞台の幅に収まる
    /// An interior machine with many outputs stays within the stage width
    /// </summary>
    public class CleanRoomWideProcessRowTest : CleanRoomLayoutTestHost
    {
        private const float PixelSnap = 0.6f;

        [TestCase(true)]
        [TestCase(false)]
        public void 出力6枠の室内機械でもパネルは舞台の残りの幅に収まり閉じると枠が見える(bool withRecipes)
        {
            // 精密切断機と同じ枠（入1・出6・モ3）
            // Same slots as the precision cutter (1 in, 6 out, 3 modules)
            var shell = OpenCleanRoomMachine(true, new MachineSlotLayout(1, 6, 3), withRecipes);
            Assert.AreEqual(withRecipes, shell.View.ClassListContains(BlockInventoryView.LargeClassName), "large only with recipe rows");

            // 持ち物パネル幅を左の列へ置く（本番同様）
            // Put an inventory-panel-wide box in the left column as in production
            var inventory = new VisualElement();
            inventory.style.width = ThemeTokenFile.ResolvePixels("--inventory-panel-width");
            inventory.style.height = 100f;
            shell.Stage.Get(StageColumn.Inv).Add(inventory);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            var stage = shell.Stage.worldBound;
            var row = shell.View.Q("machine-process-row");
            var input = row.Q(className: "machine-process-row__input");
            var output = row.Q(className: "machine-process-row__output");
            var inputGrid = shell.View.Q("machine-input-slots");
            var outputGrid = shell.View.Q("machine-output-slots");
            var arrow = row.Q<ProgressArrowGlyph>("machine-progress-arrow");
            var close = shell.View.Q("block-inventory-close").worldBound;
            var measured = $"stage={stage} shell={shell.View.worldBound} panel={shell.Panel().worldBound} row={row.worldBound} input={input.worldBound}/{inputGrid.worldBound} arrow={arrow.worldBound} output={output.worldBound}/{outputGrid.worldBound} close={close}";

            // パネル右端は舞台の右 padding 内
            // The panel's right edge stays inside the stage's right padding
            var stageStyle = shell.Stage.resolvedStyle;
            var gap = ThemeTokenFile.ResolvePixels("--stage-column-gap");
            var contentRight = stage.xMax - stageStyle.paddingRight - (withRecipes ? 0f : gap);
            Assert.LessOrEqual(shell.View.worldBound.xMax, contentRight + PixelSnap, measured);
            Assert.LessOrEqual(close.xMax, stage.xMax + PixelSnap, measured);

            // 入力・矢印・出力は重ならない
            // Input, arrow and output never overlap
            Assert.AreEqual(outputGrid.layout.width, output.layout.width, PixelSnap, measured);
            Assert.LessOrEqual(inputGrid.worldBound.xMax, arrow.worldBound.xMin + PixelSnap, measured);
            Assert.GreaterOrEqual(outputGrid.worldBound.xMin + PixelSnap, arrow.worldBound.xMax, measured);

            // 出力は外殻内、入力は行の左端内
            // Output stays inside the shell, input inside the row's left edge
            Assert.LessOrEqual(outputGrid.worldBound.xMax, shell.View.worldBound.xMax + PixelSnap, measured);
            Assert.GreaterOrEqual(inputGrid.worldBound.xMin + PixelSnap, row.worldBound.xMin, measured);
            UnityEngine.Debug.Log(measured);
        }
    }
}
