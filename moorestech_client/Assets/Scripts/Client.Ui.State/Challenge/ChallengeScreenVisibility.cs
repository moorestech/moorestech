using Client.Ui.State.Routing;

namespace Client.Ui.State.Challenge
{
    /// <summary>
    /// チャレンジ一覧を出すか（画面ルータが ChallengeList。前例 BlockInventoryScreenVisibility）
    /// Whether to show the challenge list (router on ChallengeList; precedent BlockInventoryScreenVisibility)
    /// </summary>
    public static class ChallengeScreenVisibility
    {
        public static bool IsShown(UiRoutingSnapshot snapshot)
        {
            return snapshot.Screen == UiScreen.ChallengeList;
        }
    }
}
