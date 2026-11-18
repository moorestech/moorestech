using Client.Ui.Screens.Toast;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Shared
{
    // トースト層（GameUi）の登録
    // Registers the GameUi toast layer
    internal static class ToastGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.RegisterEntryPoint<ToastLayerPresenter>();
        }
    }
}
