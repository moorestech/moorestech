using Core.Update;
using Game.Block.Interface;
using Game.EnergySystem;
using Game.Gear.Common;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Boot.DependencyInjection
{
    internal static class MoorestechServerTickRegistration
    {
        public static void Register(ServiceProvider provider)
        {
            // 現行ハンドラーを同じ解決順で取得する
            // Resolve the current handlers in the same order.
            var electricWireNetworkDatastore = provider.GetRequiredService<IElectricWireNetworkDatastore>();
            var gearNetworkDatastoreInstance = provider.GetRequiredService<GearNetworkDatastore>();
            var blockRemovalReservationService = provider.GetRequiredService<IBlockRemovalReservationService>();

            // tick更新順を電力反映・gear反映・電力計算・gear計算のまま維持する
            // Preserve tick order as electric flush, gear flush, electric update, then gear update.
            GameUpdater.AdditionalUpdates.Add(electricWireNetworkDatastore.FlushPendingCommands);
            GameUpdater.AdditionalUpdates.Add(gearNetworkDatastoreInstance.FlushPendingMutations);
            GameUpdater.AdditionalUpdates.Add(provider.GetRequiredService<ElectricTickUpdater>().Update);
            GameUpdater.AdditionalUpdates.Add(provider.GetRequiredService<GearTickUpdater>().Update);

            // tick末尾の破壊反映と両トポロジ反映を同じクロージャーで維持する
            // Preserve removal application and both topology flushes in the same tick-end closure.
            GameUpdater.TickEndUpdates.Add(() =>
            {
                blockRemovalReservationService.ApplyReservedRemovals();
                electricWireNetworkDatastore.FlushPendingCommands();
                gearNetworkDatastoreInstance.FlushPendingMutations();
            });
        }
    }
}
