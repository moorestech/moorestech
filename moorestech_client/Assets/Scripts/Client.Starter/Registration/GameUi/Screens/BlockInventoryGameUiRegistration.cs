using Client.Ui.Screens.BlockInventory;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Panel;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Screens
{
    // ブロックインベントリ画面（GameUi）の登録。後続レーンが ctor で受ける共有サービスは MainGameUiRegistration の共有サービスの並びにある（E-1・A-56）。差し込み口は後続レーンが自分の登録ファイルで同じインターフェイスを足す（D-1）
    // Registers the block-inventory screen for GameUi; services other lanes take in their ctors live in MainGameUiRegistration's shared list (E-1, A-56); later lanes add the port interfaces from their own registration files (D-1)
    internal static class BlockInventoryGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.RegisterEntryPoint<BlockInventoryPanelLayoutModel>().AsSelf();
            builder.Register<BlockInventoryPanelCommands>(Lifetime.Singleton).As<IBlockInventoryPanelCommands>();
            builder.Register<SectionStackSlotRegistry>(Lifetime.Singleton);
            builder.Register<SectionStackBodyFactory>(Lifetime.Singleton);
            builder.Register<BlockInventoryPartRegistry>(Lifetime.Singleton);
            builder.RegisterEntryPoint<BlockInventoryScreenPresenter>();
        }
    }
}
