using Client.Input.ArrowKeys;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.SlotRestriction;
using Client.Ui.State.BlockInventory.Special;
using Client.Ui.State.BlockInventory.Special.Commands;
using Client.Ui.State.BlockInventory.Special.Source;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Screens
{
    // ブロックインベントリの特殊族（GameUi）の登録。詳細の Model・操作・矢印キーと、S4-alpha の拡張口への差し込み
    // Registers the block-inventory special family for GameUi: detail model, commands, arrow keys and the plug-ins to S4-alpha's ports
    internal static class BlockInventorySpecialGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            // 詳細と操作（State）
            // Details and commands (State)
            builder.RegisterEntryPoint<SpecialBlockDetailModel>().As<ISpecialBlockDetailModel>();
            builder.Register<PanelOpenSpecialBlock>(Lifetime.Singleton).As<IOpenSpecialBlock>();
            builder.Register<VanillaSpecialBlockRequestSender>(Lifetime.Singleton).As<ISpecialBlockRequestSender>();
            builder.Register<SpecialBlockCommands>(Lifetime.Singleton).As<ISpecialBlockCommands>();
            builder.Register<SpecialInletBindingContributor>(Lifetime.Singleton).As<IBlockSlotRestrictionContributor>();

            // 矢印キー・View の依存の束・Presenter の組み立て（パネルの源と格子の工場は alpha の共有サービスを受ける）
            // Arrow keys, the view dependency bundle and presenter assembly (panel source and grid factory come from alpha's shared services)
            builder.Register<InputSystemArrowKeyStream>(Lifetime.Singleton).As<IHorizontalArrowKeySource>();
            builder.Register<SpecialViewKit>(Lifetime.Singleton);
            builder.Register<SpecialPresenters>(Lifetime.Singleton);

            // S4-alpha の拡張口 (1)・(2)・(3) への差し込み
            // Plug-ins to S4-alpha's extension ports (1), (2) and (3)
            builder.Register<FilterSplitterBodyFactory>(Lifetime.Singleton).As<IBlockInventoryBodyFactory>();
            builder.Register<ElectricToGearBodyFactory>(Lifetime.Singleton).As<IBlockInventoryBodyFactory>();
            builder.Register<ElectricPoleBodyFactory>(Lifetime.Singleton).As<IBlockInventoryBodyFactory>();
            builder.Register<SpaceElevatorBodyFactory>(Lifetime.Singleton).As<IBlockInventoryBodyFactory>();
            builder.Register<RocketLaunchPadBodyFactory>(Lifetime.Singleton).As<IBlockInventoryBodyFactory>();
            builder.Register<RocketLaunchPadTitleActionFactory>(Lifetime.Singleton).As<IBlockInventoryTitleActionFactory>();
            builder.Register<GearToElectricSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<BeltThroughputSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<GearSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<ElectricNetworkSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<GearNetworkSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
        }
    }
}
