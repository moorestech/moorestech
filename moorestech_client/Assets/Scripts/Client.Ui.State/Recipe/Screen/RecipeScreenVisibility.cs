using Client.Ui.State.Routing;

namespace Client.Ui.State.Recipe.Screen
{
    /// <summary>
    /// レシピ画面を出すかの判定の 1 か所（前例 BlockInventoryScreenVisibility。A-248）
    /// The single recipe screen visibility predicate (precedent BlockInventoryScreenVisibility; A-248)
    /// </summary>
    public static class RecipeScreenVisibility
    {
        // レシピ画面が出ているかの唯一の判定（Model の組み直しと Presenter の出し入れが共有する。Web App.tsx の screen === "playerInventory"）
        // The single check for whether the recipe screen is shown (shared by the Model rebuild and the Presenter visibility; Web App.tsx)
        public static bool IsShown(UiRoutingSnapshot snapshot)
        {
            return snapshot.Screen == UiScreen.PlayerInventory;
        }
    }
}
