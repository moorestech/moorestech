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
    /// 出力の多い室内機械でもパネルは舞台の残りの幅に収まる（Web の stage grid の auto の列・.panelLarge の幅・processRow の grid）
    /// An interior machine with many outputs stays within the stage's remaining width (Web stage grid auto track, .panelLarge width, processRow grid)
    /// </summary>
    public class CleanRoomWideProcessRowTest : CleanRoomLayoutTestHost
    {
        private const float PixelSnap = 0.6f;

        [TestCase(true)]
        [TestCase(false)]
        public void 出力6枠の室内機械でもパネルは舞台の残りの幅に収まり閉じると枠が見える(bool withRecipes)
        {
            // 精密切断機と同じ 入力 1・出力 6・モジュール 3。レシピが無ければ小型（自然高）の面
            // Same as the precision cutter: 1 input, 6 outputs, 3 modules; without recipes it is the natural-height panel
            var shell = OpenCleanRoomMachine(2, new MachineSlotLayout(1, 6, 3), withRecipes);
            Assert.AreEqual(withRecipes, shell.View.ClassListContains(BlockInventoryView.LargeClassName), "large only with recipe rows");

            // 本番と同じく持ち物パネルの幅を左の列に置く（SubInventory では持ち物が左に開いている）
            // Put an inventory-panel-wide box in the left column as in production (SubInventory shows the inventory on the left)
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

            // パネルの右端は舞台の右の padding の内側（大型は viewer〜items、小型は viewer の列の残りの幅）
            // The panel's right edge stays inside the stage's right padding (large spans viewer..items, natural keeps the viewer track's remaining width)
            var stageStyle = shell.Stage.resolvedStyle;
            var gap = ThemeTokenFile.ResolvePixels("--stage-column-gap");
            var contentRight = stage.xMax - stageStyle.paddingRight - (withRecipes ? 0f : gap);
            Assert.LessOrEqual(shell.View.worldBound.xMax, contentRight + PixelSnap, measured);
            Assert.LessOrEqual(close.xMax, stage.xMax + PixelSnap, measured);

            // Web の grid は出力側を中身の幅に保ち、入力側が残りを取る。入力・矢印・出力は重ならない
            // The Web grid keeps the output side content-wide and gives the rest to the input side; input, arrow and output never overlap
            Assert.AreEqual(outputGrid.layout.width, output.layout.width, PixelSnap, measured);
            Assert.LessOrEqual(inputGrid.worldBound.xMax, arrow.worldBound.xMin + PixelSnap, measured);
            Assert.GreaterOrEqual(outputGrid.worldBound.xMin + PixelSnap, arrow.worldBound.xMax, measured);
            UnityEngine.Debug.Log(measured);
        }
    }
}
