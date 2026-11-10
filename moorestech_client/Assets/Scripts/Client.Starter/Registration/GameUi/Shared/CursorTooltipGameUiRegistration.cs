using Client.Ui.Screens.CursorTooltip;
using Client.Ui.State.CursorTooltip;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Shared
{
    // カーソルツールチップ（GameUi）の登録
    // Registers the GameUi cursor tooltip
    internal static class CursorTooltipGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.RegisterEntryPoint<CursorTooltipModel>().AsSelf();
            builder.Register<UnityCursorTooltipScreen>(Lifetime.Singleton).As<ICursorTooltipScreen>();
            builder.RegisterEntryPoint<CursorTooltipPresenter>();
        }
    }
}
