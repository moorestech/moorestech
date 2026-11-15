using Client.Ui.State.Recipe.Catalog;
using Client.Ui.State.Recipe.Commands;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions;
using Client.WebUiHost.Game.Topics;
using Game.UnlockState;

namespace Client.WebUiHost.Game
{
    public static class RecipeWebUiRegistration
    {
        public static void Register(WebSocketHub hub, IGameUnlockStateData unlockStateData, RecipeViewerItemCatalog recipeViewerItemCatalog, IRecipeCommands recipeCommands)
        {
            // レシピ画面のtopicとクラフトのactionを同じ境界で登録する。判定はGameUiと同じStateのCatalog・型付きコマンドを通す
            // Register the recipe-screen topics and the craft action at one boundary; decisions go through the same State catalogs and typed commands as GameUi
            hub.RegisterTopic(CraftingRecipesTopic.TopicName, new CraftingRecipesTopic(hub, unlockStateData));
            hub.RegisterTopic(RecipeViewerItemListTopic.TopicName, new RecipeViewerItemListTopic(hub, recipeViewerItemCatalog));
            hub.RegisterAction(new CraftExecuteActionHandler(recipeCommands));
        }
    }
}
