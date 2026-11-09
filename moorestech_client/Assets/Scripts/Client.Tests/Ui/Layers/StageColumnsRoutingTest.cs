using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.State.Routing;
using NUnit.Framework;

namespace Client.Tests.Ui.Layers
{
    /// <summary>
    /// 左の余白を畳むのは研究画面だけ（Web の .stageResearch）
    /// Only the research screen collapses the left padding (Web .stageResearch)
    /// </summary>
    public class StageColumnsRoutingTest
    {
        [TestCase(UiScreen.None, false)]
        [TestCase(UiScreen.PlayerInventory, false)]
        [TestCase(UiScreen.SubInventory, false)]
        [TestCase(UiScreen.ResearchTree, true)]
        [TestCase(UiScreen.BuildMenu, false)]
        [TestCase(UiScreen.ChallengeList, false)]
        [TestCase(UiScreen.PauseMenu, false)]
        [TestCase(UiScreen.TrainHud, false)]
        [TestCase(UiScreen.TrainPause, false)]
        [TestCase(UiScreen.SkitPause, false)]
        public void 研究画面だけが左の余白を畳む(UiScreen screen, bool expected)
        {
            Assert.AreEqual(expected, StageColumnsRouting.CollapsesLeftPadding(screen));
        }
    }
}
