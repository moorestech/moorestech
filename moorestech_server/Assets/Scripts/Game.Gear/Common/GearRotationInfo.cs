namespace Game.Gear.Common
{
    public class GearRotationInfo
    {
        public readonly IGearEnergyTransformer EnergyTransformer;
        public readonly float RpmRatio;
        public readonly bool IsClockwise;
        public RPM Rpm { get; private set; }
        public Torque RequiredTorque { get; private set; }

        public GearRotationInfo(IGearEnergyTransformer energyTransformer, float rpmRatio, bool isClockwise)
        {
            EnergyTransformer = energyTransformer;
            RpmRatio = rpmRatio;
            IsClockwise = isClockwise;
            Rpm = new RPM(0);
            RequiredTorque = new Torque(0);
        }

        public void SetRpm(RPM rpm)
        {
            Rpm = rpm;
        }

        public void SetRequiredTorque(Torque requiredTorque)
        {
            RequiredTorque = requiredTorque;
        }
    }
}
