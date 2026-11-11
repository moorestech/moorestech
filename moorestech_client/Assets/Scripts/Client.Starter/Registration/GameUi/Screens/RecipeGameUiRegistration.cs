using Client.Ui.Screens.Recipe;
using Client.Ui.Screens.Recipe.Input;
using Client.Ui.State.Recipe.Catalog;
using Client.Ui.State.Recipe.Commands;
using Client.Ui.State.Recipe.Screen;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Screens
{
    // レシピ画面（ビューアと一覧）の登録。解放済み機械レシピの源 UnlockedMachineRecipeSource は MainGameUiRegistration の共有サービスが持つ
    // Registers the recipe screen (viewer and list); the UnlockedMachineRecipeSource is owned by MainGameUiRegistration's shared services
    internal static class RecipeGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            // State: 選択・カタログ・モデル・操作。モデルは Initialize で購読するので entry point（前例 ResearchTreeModel）
            // State: selection, catalog, model and commands; the model subscribes in Initialize, so it is an entry point (precedent ResearchTreeModel)
            builder.Register<RecipeItemSelection>(Lifetime.Singleton);
            builder.Register<RecipeViewerItemCatalog>(Lifetime.Singleton);
            builder.RegisterEntryPoint<RecipeScreenModel>().AsSelf();
            builder.Register<VanillaApiCraftRequestSender>(Lifetime.Singleton).As<ICraftRequestSender>();
            builder.Register<RecipeCommands>(Lifetime.Singleton).As<IRecipeCommands>();

            // Esc の選択解除は UiRoutingState を直に受ける entry point（前例 ResearchSelectionStore）
            // Esc clearing is an entry point taking UiRoutingState directly (precedent ResearchSelectionStore)
            builder.Register<InputManagerCloseKeyProbe>(Lifetime.Singleton).As<IUiCloseKeyProbe>();
            builder.RegisterEntryPoint<RecipeSelectionCloseReset>();

            builder.RegisterEntryPoint<RecipeScreenPresenter>();
        }
    }
}
