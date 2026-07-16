namespace Game.EnergySystem
{
    // 電力網の登録状態とtick境界更新だけを変更する契約
    // Mutation contract for electric registration and tick-boundary updates
    public interface IElectricWireNetworkMutation
    {
        void AddConnector(IElectricWireConnector connector);
        void RemoveConnector(IElectricWireConnector connector);
        void MarkTopologyDirty();
        void MarkStatisticsDirty();
        void RebuildIfDirty();
        void MarkStatisticsSettled();
    }
}
