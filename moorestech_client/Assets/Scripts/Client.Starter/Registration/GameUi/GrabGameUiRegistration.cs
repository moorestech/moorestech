using Client.Input.Pointer;
using Client.Ui.Screens.GrabOverlay;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Drag;
using Client.Ui.State.Inventory.Grab;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi
{
    // 掴み・分割ドラッグ・掴み絵（GameUi）の登録。ブロック側の実行器とスロット操作は S4-alpha が MainGameUiRegistration の共有サービスの並びに足す
    // Registers grab, split drag and the grab picture for GameUi; the block-side executor and slot commands are added by S4-alpha to MainGameUiRegistration's shared service list
    internal static class GrabGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.Register<LocalPlayerInventoryOperations>(Lifetime.Singleton).As<IPlayerInventoryOperations>();
            builder.Register<PlayerItemActionExecutor>(Lifetime.Singleton);
            builder.RegisterEntryPoint<PlayerGrabModel>().AsSelf();
            builder.RegisterEntryPoint<GrabOverlayModel>().AsSelf();
            builder.Register<InputSystemPointerButtonStream>(Lifetime.Singleton);
            builder.Register<InputSystemPrimaryPointerButton>(Lifetime.Singleton).As<IPrimaryPointerButton>();

            // 解放の購読（確定の持ち主）と S4-alpha が呼ぶ口を同じ 1 個にする（D-26）
            // The release subscription (commit owner) and the port S4-alpha calls are the same single instance (D-26)
            builder.RegisterEntryPoint<PlayerSlotDragCommands>().As<IPlayerSlotDragCommands>();
            builder.RegisterEntryPoint<GrabOverlayPresenter>();
        }
    }
}
