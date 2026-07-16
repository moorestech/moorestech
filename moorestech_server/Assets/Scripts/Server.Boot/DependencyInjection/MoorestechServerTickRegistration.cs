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
            // 両網再構築後に需給計算する
            // Apply both topologies from the same world boundary before either settlement begins
            GameUpdater.AdditionalUpdates.Add(
                provider.GetRequiredService<IElectricWireNetworkDatastore>().RebuildIfDirty);
            GameUpdater.AdditionalUpdates.Add(
                provider.GetRequiredService<GearNetworkDatastore>().RebuildIfDirty);
            GameUpdater.AdditionalUpdates.Add(
                provider.GetRequiredService<ElectricTickUpdater>().Update);
            GameUpdater.AdditionalUpdates.Add(
                provider.GetRequiredService<GearTickUpdater>().Update);

            // tick末尾は破壊だけを確定する
            // Commit reserved removals only at tick end and defer derived-network rebuilding to the next tick head
            var blockRemovalReservationService = provider.GetRequiredService<IBlockRemovalReservationService>();
            GameUpdater.TickEndUpdates.Add(blockRemovalReservationService.ApplyReservedRemovals);
        }
    }
}
