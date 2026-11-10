using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.Screens.BlockInventory.Family;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Machine;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Family.Models;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Selection;
using Client.Ui.State.BlockInventory.SlotRestriction;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Screens
{
    // 機械と汎用族の区画・詳細 Model・alpha の口への寄与を登録する（S4-machine）。液体の共有サービスは MainGameUiRegistration の共有サービスの並びにある（E-1）
    // Registers the machine and family sections, detail models and contributions to alpha's ports (S4-machine); shared fluid services live in MainGameUiRegistration's shared list (E-1)
    internal static class BlockInventoryMachineGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            // 詳細 Model（追跡ブロックを購読して配る。IInitializable なので入口として登録する）
            // Detail models (subscribe the tracked block and publish; IInitializable, so registered as entry points)
            builder.RegisterEntryPoint<MachineSectionModel>().As<IMachineSectionModel>();
            builder.RegisterEntryPoint<MinerDetailModel>().As<IMinerDetailModel>();
            builder.RegisterEntryPoint<PumpDetailModel>().As<IPumpDetailModel>();
            builder.RegisterEntryPoint<GeneratorDetailModel>().As<IGeneratorDetailModel>();
            builder.RegisterEntryPoint<BlockContextModel>().As<IBlockContextModel>();

            // 選ぶコマンド（送信はコマンドが ClientContext.VanillaApi を直接 await する。D-40）
            // The select command (it awaits ClientContext.VanillaApi directly; D-40)
            builder.Register<MachineRecipeSelectCommand>(Lifetime.Singleton).As<IMachineRecipeSelectCommand>();

            // alpha の口への寄与（大型の判定は入力の変化を自分で絞るので入口として登録する）
            // Contributions to alpha's ports (the large-panel rule filters its own input changes, so it is an entry point)
            builder.RegisterEntryPoint<RecipeMachineLargePanelRule>();
            builder.Register<MachineSlotRestrictionContributor>(Lifetime.Singleton).As<IBlockSlotRestrictionContributor>();

            // 区画の工場（Slot ごとに 1 本）
            // Section factories (one per slot)
            builder.Register<ContextSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<MachineSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<MinerSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<PumpSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<GeneratorSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<FluidDetailsSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<FluidRowSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
        }
    }
}
