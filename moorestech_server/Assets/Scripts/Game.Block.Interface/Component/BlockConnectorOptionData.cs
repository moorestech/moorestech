namespace Game.Block.Interface.Component
{
    public readonly struct GearConnectOptionData
    {
        public readonly bool IsReverse;

        public GearConnectOptionData(bool isReverse)
        {
            IsReverse = isReverse;
        }
    }

    public readonly struct FluidConnectOptionData
    {
        public readonly double FlowCapacity;
        public readonly int ConnectTankIndex;

        public FluidConnectOptionData(double flowCapacity, int connectTankIndex)
        {
            FlowCapacity = flowCapacity;
            ConnectTankIndex = connectTankIndex;
        }
    }
}
