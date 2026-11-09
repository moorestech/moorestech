using Client.Ui.Components.Text;
using Client.Ui.Foundation.Input.Pointer;
using Client.Ui.Foundation.Modal;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Foundation.Tooltip;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi
{
    /// <summary>
    /// GameUi の土台サービスを登録
    /// Registers the GameUi foundation services
    /// </summary>
    internal static class FoundationGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            // 倍率を読むのは監視の 1 点だけ。読み口はその流れを包んで配る
            // Only the watch reads the scale; the source wraps its stream for consumers
            builder.RegisterEntryPoint<GameUiStageScaleWatch>().AsSelf();
            builder.Register(resolver => new StageScaleSource(resolver.Resolve<GameUiStageScaleWatch>().Scale), Lifetime.Singleton);

            // 画面追従 UI 共通のポインタ（1個）
            // One pointer for screen-following UI (grab picture, cursor tooltip)
            builder.Register<InputSystemUiScreenPointer>(Lifetime.Singleton).As<IUiScreenPointer>();

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
