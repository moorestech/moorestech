using Client.Input.Pointer;
using Client.Ui.Screens.Inventory.Equipment;
using Client.Ui.Screens.Inventory.Panel;
using Client.Ui.Screens.Inventory.Wheel;
using Client.Ui.State.Inventory.Equipment;
using Client.Ui.State.Inventory.Panel;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Screens
{
    // 持ち物・装備 HUD・装備ホイールの登録。枠コマンドは MainGameUiRegistration
    // Registers inventory, equipment HUD and wheel; slot commands live in MainGameUiRegistration
    internal static class InventoryGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.Register<LocalPlayerEquipmentSelectionPort>(Lifetime.Singleton).As<IEquipmentSelectionPort>();
            builder.RegisterEntryPoint<PlayerInventoryPanelModel>().AsSelf();
            builder.Register<InventorySortCommands>(Lifetime.Singleton).As<IInventorySortCommands>();
            builder.RegisterEntryPoint<EquipmentWheelSelector>().AsSelf();
            builder.Register<EquipmentWheelRouter>(Lifetime.Singleton);
            builder.Register<InputSystemPointerWheelStream>(Lifetime.Singleton);
            builder.Register<EquipmentWheelPointerPicker>(Lifetime.Singleton).As<IEquipmentWheelPointerPicker>();
            builder.RegisterEntryPoint<EquipmentWheelInput>();
            builder.RegisterEntryPoint<InventoryScreenPresenter>();
            builder.RegisterEntryPoint<EquipmentHudPresenter>();
        }
    }
}
