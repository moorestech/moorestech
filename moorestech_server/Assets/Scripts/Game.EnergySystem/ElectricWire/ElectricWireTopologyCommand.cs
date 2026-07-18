namespace Game.EnergySystem
{
    // 保留中のワイヤートポロジ変更1件。FIFOで一括適用される
    // One pending wire topology mutation, applied in FIFO batches
    public readonly struct ElectricWireTopologyCommand
    {
        public readonly ElectricWireTopologyCommandType CommandType;
        public readonly IElectricWireConnector[] Connectors;

        public ElectricWireTopologyCommand(ElectricWireTopologyCommandType commandType, IElectricWireConnector[] connectors)
        {
            CommandType = commandType;
            Connectors = connectors;
        }
    }

    public enum ElectricWireTopologyCommandType
    {
        Add,
        Remove,
        // 対象群を除去→再追加して連結成分を再計算する周辺再構築
        // Rebuild around the targets: remove then re-add to recompute connected components
        Rebuild,
    }
}
