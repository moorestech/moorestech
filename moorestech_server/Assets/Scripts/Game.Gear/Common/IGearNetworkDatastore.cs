using Game.Block.Interface;
using Game.Gear.Tick;

namespace Game.Gear.Common
{
    // live gearと適用済みgear網を分離する。dirty再構築はtick先頭で具象datastore経由（MasterTickUpdater）
    // Separates live gear registration from applied networks; the dirty rebuild runs at tick head via the concrete datastore (MasterTickUpdater)
    public interface IGearNetworkDatastore
    {
        void AddGear(IGearEnergyTransformer gear);
        void RemoveGear(IGearEnergyTransformer gear);
        void MarkTopologyDirty();
        void NotifyGeneratorOutputChanged(IGearEnergyTransformer generator);
        void NotifyConsumerDemandChanged(IGearEnergyTransformer consumer);
        void RegisterOverloadTickTarget(IGearOverloadTickTarget target);
        void UnregisterOverloadTickTarget(IGearOverloadTickTarget target);
        bool TryGetGearNetwork(BlockInstanceId blockInstanceId, out GearNetwork network);
        GearNetwork GetGearNetwork(BlockInstanceId blockInstanceId);
    }
}
