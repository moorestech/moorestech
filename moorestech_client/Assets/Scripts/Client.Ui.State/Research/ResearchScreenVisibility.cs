using Client.Ui.State.Routing;

namespace Client.Ui.State.Research
{
    /// <summary>
    /// 研究画面を出すか（画面ルータが ResearchTree。判定式の置き場はここ 1 か所）
    /// Whether to show the research screen (router on ResearchTree; the single home of this predicate)
    /// </summary>
    public static class ResearchScreenVisibility
    {
        public static bool IsShown(UiRoutingSnapshot snapshot)
        {
            return snapshot.Screen == UiScreen.ResearchTree;
        }
    }
}
