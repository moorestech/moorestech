namespace Core.BeltTransport
{
    public sealed class BeltTickOrder
    {
        public readonly uint ServerTick, SimulationSequenceId, CompletedSequenceId;
        public readonly uint[] BeforeSequenceIds, AfterSequenceIds;

        public BeltTickOrder(uint serverTick, uint[] beforeSequenceIds, uint simulationSequenceId, uint[] afterSequenceIds, uint completedSequenceId)
        {
            ServerTick = serverTick;
            BeforeSequenceIds = beforeSequenceIds;
            SimulationSequenceId = simulationSequenceId;
            AfterSequenceIds = afterSequenceIds;
            CompletedSequenceId = completedSequenceId;
        }
    }
}
