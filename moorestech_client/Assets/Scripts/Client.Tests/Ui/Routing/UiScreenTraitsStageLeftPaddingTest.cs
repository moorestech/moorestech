using Client.Ui.State.Routing;
using NUnit.Framework;

namespace Client.Tests.Ui.Routing
{
    /// <summary>
    /// 画面の性質の表で舞台の左の余白を畳むのは研究画面だけ（Web の .stageResearch）
    /// In the screen-trait table only the research screen collapses the stage left padding (Web .stageResearch)
    /// </summary>
    public class UiScreenTraitsStageLeftPaddingTest
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
            Assert.AreEqual(expected, UiScreenTraitsTable.Get(screen).CollapsesStageLeftPadding);
        }
    }
}
