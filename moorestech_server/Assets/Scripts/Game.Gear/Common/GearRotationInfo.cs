namespace Game.Gear.Common
{
    public class GearRotationInfo
    {
        public readonly float RpmRatio;
        public readonly bool IsClockwise;
        public readonly IGearEnergyTransformer EnergyTransformer;

        public GearRotationInfo(float rpmRatio, bool isClockwise, IGearEnergyTransformer energyTransformer)
        {
            RpmRatio = rpmRatio;
            IsClockwise = isClockwise;
            EnergyTransformer = energyTransformer;
        }

        public RPM GetRpm(RPM originRpm)
        {
            return new RPM(originRpm.AsPrimitive() * RpmRatio);
        }
    }
}
