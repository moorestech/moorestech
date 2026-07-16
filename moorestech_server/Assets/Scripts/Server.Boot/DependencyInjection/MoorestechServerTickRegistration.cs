using Core.Update;
using Game.EnergySystem;
using Game.Gear.Common;
using Game.SaveLoad;
using Microsoft.Extensions.DependencyInjection;
using Server.Boot.Loop.PacketProcessing;

namespace Server.Boot.DependencyInjection
{
    internal static class MoorestechServerTickRegistration
    {
        public static void Register(ServiceProvider provider)
        {
            // 両網再構築後に需給計算する
            // Apply both topologies from the same world boundary before either settlement begins
            GameUpdater.AdditionalUpdates.Add(
                provider.GetRequiredService<IElectricWireNetworkMutation>().RebuildIfDirty);
            GameUpdater.AdditionalUpdates.Add(
                provider.GetRequiredService<GearNetworkDatastore>().RebuildIfDirty);
            GameUpdater.AdditionalUpdates.Add(
                provider.GetRequiredService<ElectricTickUpdater>().Update);
            GameUpdater.AdditionalUpdates.Add(
                provider.GetRequiredService<GearTickUpdater>().Update);

            // tick末尾は固定入力と予約破壊を一つの更新器で確定する
            // Commit frozen input and reserved removals through one tick-end updater
            GameUpdater.TickEndUpdates.Add(
                provider.GetRequiredService<WorldMutationTickEndUpdater>().Update);

            // 世界変更をすべて確定した後で、要求済みの保存を一度だけ実行する
            // Execute a requested save once after every world mutation has been committed
            GameUpdater.FinalTickEndUpdates.Add(
                provider.GetRequiredService<WorldSaveCoordinator>().SaveIfRequested);
        }
    }
}
