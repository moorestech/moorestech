using Client.Input.Pointer;
using Client.Ui.Screens.CursorTooltip;
using Client.Ui.State.CursorTooltip;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi
{
    // カーソルツールチップ（GameUi）の登録。実画面の問い合わせとポインタ位置の購読口は本番の実装を当てる
    // Registers the GameUi cursor tooltip; the screen query and pointer position stream use the production implementations
    internal static class CursorTooltipGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.RegisterEntryPoint<CursorTooltipModel>().AsSelf();
            builder.Register<InputSystemPointerPositionStream>(Lifetime.Singleton).As<IPointerPositionStream>();
            builder.Register<UnityCursorTooltipScreen>(Lifetime.Singleton).As<ICursorTooltipScreen>();
            builder.RegisterEntryPoint<CursorTooltipPresenter>();
        }
    }
}
