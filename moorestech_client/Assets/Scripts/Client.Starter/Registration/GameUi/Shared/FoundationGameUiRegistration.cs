using Client.Ui.Components.Text;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Input.Pointer;
using Client.Ui.Foundation.Modal;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Foundation.Tooltip;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Shared
{
    /// <summary>
    /// GameUi の土台サービスを登録
    /// Registers the GameUi foundation services
    /// </summary>
    internal static class FoundationGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            // 画面追従 UI 共通のポインタ（1個）
            // One pointer for screen-following UI (grab picture, cursor tooltip)
            builder.Register<InputSystemUiScreenPointer>(Lifetime.Singleton).As<IUiScreenPointer>();

            // OS カーソルの形を要求する口。1 本であることは ProcessUiOsCursor の static が保証する。画面が ctor で受けて部品へ渡す
            // Port requesting the OS cursor shape; ProcessUiOsCursor's static guarantees one stack; screens take it by ctor and pass it to parts
            builder.Register<UnityUiOsCursorApplier>(Lifetime.Singleton).As<IUiOsCursorApplier>();
            builder.Register<ProcessUiOsCursor>(Lifetime.Singleton).As<IUiOsCursor>();

            // モーダルの宿主は画面が ctor で受け、入口が Modal 層へ置く
            // Screens take the modal host by ctor; the entry point mounts it in the Modal layer
            builder.Register<UiModalHost>(Lifetime.Singleton);
            builder.RegisterEntryPoint<GameUiModalHostInstaller>();

            // ホバーの面は Components の TooltipFace（Foundation は口だけを知る）
            // The hover face is the Components TooltipFace (Foundation only knows the interface)
            builder.RegisterInstance<IHoverTooltipFace>(new TooltipFace("hover-tooltip", TooltipFaceLook.Hover));
            builder.RegisterEntryPoint<GameUiHoverTooltipInstaller>();

            // 舞台の上段 3 列は画面が ctor で受けて列へ載せ、入口が舞台の Screen 層へ置く
            // Screens take the stage columns by ctor and add into a column; the entry point mounts it on the stage Screen layer
            builder.Register<StageColumnsLayout>(Lifetime.Singleton);
            builder.RegisterEntryPoint<GameUiStageColumnsInstaller>();
        }
    }
}
