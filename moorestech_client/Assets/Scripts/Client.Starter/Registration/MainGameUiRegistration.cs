using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Routing;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BugReport;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.Screens.Toast;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Settings;
using Client.Ui.State.Toast;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration
{
    // Client.Ui の登録口。後続レーンは画面の Presenter・Mount をこの下に 1 行ずつ足す
    // Registration entry for Client.Ui; later lanes append their screen presenters and mounts below
    internal static class MainGameUiRegistration
    {
        public static void Register(ContainerBuilder builder, GameUiRoot gameUiRoot)
        {
            builder.RegisterComponent(gameUiRoot);
            builder.RegisterEntryPoint<WebUiSuspension>();
            builder.RegisterEntryPoint<GameUiLocaleClassBinder>();
            builder.RegisterEntryPoint<GameUiInputInstaller>();
            builder.RegisterEntryPoint<ImeCompositionModeApplier>();

            // 画面の状態と型付きコマンド。Web のアダプタと GameUi の View が同じものを使う
            // Screen state and typed commands, shared by the Web adapters and the GameUi views
            builder.Register<PauseMenuCommands>(Lifetime.Singleton).As<IPauseMenuCommands>();
            builder.Register<SettingsCommands>(Lifetime.Singleton).As<ISettingsCommands>();
            builder.RegisterEntryPoint<PauseMenuModel>().AsSelf();
            builder.RegisterEntryPoint<SettingsModel>().AsSelf();
            builder.Register<GameUiToastFeed>(Lifetime.Singleton);
            builder.RegisterEntryPoint<ToastLayerPresenter>();

            // ポーズ画面。書きかけはページの作り直しを越えて残すので Presenter の外に置く
            // The pause screen; the draft lives outside the presenter so it survives page rebuilds
            builder.Register<BugReportDraft>(Lifetime.Singleton);
            builder.RegisterEntryPoint<PauseMenuScreenPresenter>();
        }
    }
}
