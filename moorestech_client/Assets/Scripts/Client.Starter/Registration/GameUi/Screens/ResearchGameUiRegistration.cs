using Client.Ui.Screens.Research;
using Client.Ui.State.Research.Catalog;
using Client.Ui.State.Research.Commands;
using Client.Ui.State.Research.Tree;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Screens
{
    // 研究ツリー（GameUi）の登録。状態の源 ResearchNodeStateStore は MainGameUiStateRegistration が持つ
    // Registers the GameUi research tree; the ResearchNodeStateStore source lives in MainGameUiStateRegistration
    internal static class ResearchGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.Register<ResearchNodeCatalog>(Lifetime.Singleton);
            builder.RegisterEntryPoint<ResearchSelectionStore>().AsSelf();
            builder.RegisterEntryPoint<ResearchTreeModel>().AsSelf();
            builder.Register<ResearchTreeCommands>(Lifetime.Singleton).As<IResearchTreeCommands>();
            builder.RegisterEntryPoint<ResearchTreePresenter>();
        }
    }
}
