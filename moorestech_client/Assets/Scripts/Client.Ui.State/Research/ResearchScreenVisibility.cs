using Client.Ui.State.Routing;

namespace Client.Ui.State.Research
{
    /// <summary>
    /// 研究画面を出すかの判定の 1 か所
    /// The single research screen visibility predicate
    /// </summary>
    public static class ResearchScreenVisibility
    {
        public static bool IsShown(UiRoutingSnapshot snapshot)
        {
            return snapshot.Screen == UiScreen.ResearchTree;
        }
    }
}
