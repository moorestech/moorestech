using Client.Ui.State.Routing;

namespace Client.Ui.Foundation.Stage.Columns
{
    /// <summary>
    /// 左の余白を畳む画面（Web App.tsx の screen === "research" で付く .stageResearch）
    /// Screens that collapse the left padding (Web App.tsx adds .stageResearch when screen === "research")
    /// </summary>
    public static class StageColumnsRouting
    {
        public static bool CollapsesLeftPadding(UiScreen screen)
        {
            return screen == UiScreen.ResearchTree;
        }
    }
}
