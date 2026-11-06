using Client.Ui.Screens.Toast;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi
{
    // トースト層（GameUi）の登録。トーストの流れ（GameUiToastFeed）は共通の状態として登録済み
    // Registers the GameUi toast layer; the toast feed (GameUiToastFeed) is already registered as shared state
    internal static class ToastGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.RegisterEntryPoint<ToastLayerPresenter>();
        }
    }
}
