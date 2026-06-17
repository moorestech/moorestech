namespace Game.Gear.Common
{
    public class GearRotationInfo
    {
        public readonly RPM Rpm;
        public readonly bool IsClockwise;
        public readonly Torque RequiredTorque;
        public readonly IGearEnergyTransformer EnergyTransformer;

        public GearRotationInfo(RPM rpm, bool isClockwise, IGearEnergyTransformer energyTransformer)
        {
            Rpm = rpm;
            IsClockwise = isClockwise;
            EnergyTransformer = energyTransformer;
            RequiredTorque = energyTransformer.GetRequiredTorque(rpm, isClockwise);
        }
    }
}
