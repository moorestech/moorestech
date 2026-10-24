using Game.Block.Blocks.Machine.Upgrade;
using Game.Block.Interface.MachineUpgrade;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Boot.Composition
{
    /// <summary>
    ///     機械の強化系（スロット上限・製作上限）の状態を登録する
    ///     Registers the machine-upgrade state (slot limit, craft cap)
    /// </summary>
    internal static class MachineUpgradeServiceRegistration
    {
        internal static void Register(IServiceCollection services)
        {
            // 具象1つを取得側と変更側で共有する
            // One concrete instance is shared by the lookup and mutation sides
            services.AddSingleton<ModuleSlotStackLimitDataStore>();
            services.AddSingleton<IModuleSlotStackLimitLookup>(provider => provider.GetRequiredService<ModuleSlotStackLimitDataStore>());
            services.AddSingleton<IModuleSlotStackLimitUnlocker>(provider => provider.GetRequiredService<ModuleSlotStackLimitDataStore>());
            services.AddSingleton<LevelInheritanceCraftCapDataStore>();
            services.AddSingleton<ILevelInheritanceCraftCapLookup>(provider => provider.GetRequiredService<LevelInheritanceCraftCapDataStore>());
            services.AddSingleton<ILevelInheritanceCraftCapUnlocker>(provider => provider.GetRequiredService<LevelInheritanceCraftCapDataStore>());
        }
    }
}
