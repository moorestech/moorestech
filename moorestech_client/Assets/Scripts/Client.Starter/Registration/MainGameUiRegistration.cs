using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Routing;
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
            builder.RegisterEntryPoint<GameUiInputInstaller>();
            builder.RegisterEntryPoint<ImeCompositionModeApplier>();
        }
    }
}
