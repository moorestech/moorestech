using Client.Tests.Ui.Components;
using Client.Ui.Components.Gauges;
using Client.Ui.State.BlockInventory.Logic.Detail;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 出力の多い室内機械でも大型パネルは帯の幅に収まる（Web の panelLarge の幅と processRow の grid）
    /// A large interior machine with many outputs stays within the band width (Web panelLarge width and the processRow grid)
    /// </summary>
    public class CleanRoomWideProcessRowTest : CleanRoomLayoutTestHost
    {
        private const float PixelSnap = 0.6f;

        [Test]
        public void 出力6枠の室内機械でもパネルは帯の幅のままで閉じると出力枠が画面に収まる()
        {
            var shell = OpenCleanRoomMachine(2, new MachineSlotLayout(1, 6, 3));
            var stage = shell.Stage.worldBound;
            var panel = shell.Panel().worldBound;
            var row = shell.View.Q("machine-process-row");
            var outputGrid = shell.View.Q("machine-output-slots");
            var inputGrid = shell.View.Q("machine-input-slots");
            var arrow = row.Q<ProgressArrowGlyph>("machine-progress-arrow");
            var close = shell.View.Q("block-inventory-close").worldBound;
            var measured = $"stage={stage} shell={shell.View.worldBound} shellStyleW={shell.View.resolvedStyle.width} panel={panel} body={shell.Panel().contentContainer.worldBound} row={row.worldBound} input={inputGrid.worldBound} arrow={arrow.worldBound} output={outputGrid.worldBound} close={close}";

            // 大型の幅は 舞台 − 左右 padding − 持ち物パネル − 列の間（Web .panelLarge の calc）
            // The large width is stage − side paddings − inventory panel − column gap (Web .panelLarge calc)
            var stageStyle = shell.Stage.resolvedStyle;
            var expectedWidth = stageStyle.width - stageStyle.paddingLeft - stageStyle.paddingRight - ThemeTokenFile.ResolvePixels("--inventory-panel-width") - ThemeTokenFile.ResolvePixels("--stage-column-gap");
            Assert.AreEqual(expectedWidth, shell.View.layout.width, PixelSnap, measured);
            Assert.LessOrEqual(close.xMax, stage.xMax + PixelSnap, measured);

            // Web の grid は出力側を中身の幅に保ち、入力側が残りを取る。入力は矢印の手前に寄る
            // The Web grid keeps the output side content-wide and gives the rest to the input side, which hugs the arrow
            Assert.LessOrEqual(inputGrid.worldBound.xMax, arrow.worldBound.xMin + PixelSnap, measured);
            Assert.GreaterOrEqual(outputGrid.worldBound.xMin + PixelSnap, arrow.worldBound.xMax, measured);
            Assert.LessOrEqual(outputGrid.worldBound.xMax, shell.Panel().contentContainer.worldBound.xMax + PixelSnap, measured);
        }
    }
}
