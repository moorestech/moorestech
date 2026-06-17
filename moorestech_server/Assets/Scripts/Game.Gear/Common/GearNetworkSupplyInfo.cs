namespace Game.Gear.Common
{
    internal readonly struct GearNetworkSupplyInfo
    {
        public readonly IGearEnergyTransformer Transformer;
        public readonly RPM Rpm;
        public readonly bool IsClockwise;
        public readonly Torque RequiredTorque;

        public GearNetworkSupplyInfo(IGearEnergyTransformer transformer, RPM rpm, bool isClockwise, Torque requiredTorque)
        {
            Transformer = transformer;
            Rpm = rpm;
            IsClockwise = isClockwise;
            RequiredTorque = requiredTorque;
        }
    }
}
