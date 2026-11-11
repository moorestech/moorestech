using Client.Ui.Content;
using Client.Ui.Screens.BuildMenu;
using Client.Ui.State.BuildMenu.Commands;
using Client.Ui.State.BuildMenu.Session;
using Client.Ui.State.BuildMenu.Source;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Screens
{
    // ビルドメニュー（GameUi）の登録。Model・コマンド・セッションは GameUi の系だけが使うので画面と一緒にここで登録する（Web の系は橋が同じ規則で動く）
    // Registers the GameUi build menu; only the GameUi system uses the model, commands and session, so they are registered here with the screen (the Web system runs the bridge with the same rules)
    internal static class BuildMenuGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            // セッションはメニューを閉じても残す（Web の module 変数と同じ寿命）
            // The session outlives each close (same lifetime as the Web module variable)
            builder.Register<BuildMenuSessionStore>(Lifetime.Singleton);
            builder.RegisterEntryPoint<BuildMenuModel>().AsSelf();

            // IBlueprintDeleteService は MainGameInteractionRegistration の As で、PlacementTargetIcons は ContentGameUiRegistration で引ける（D-35・D-42）
            // IBlueprintDeleteService comes from the As in MainGameInteractionRegistration and PlacementTargetIcons from ContentGameUiRegistration (D-35, D-42)
            builder.Register<BuildMenuCommands>(Lifetime.Singleton).As<IBuildMenuCommands>();
            builder.RegisterEntryPoint<BuildMenuScreenPresenter>();
        }
    }
}
