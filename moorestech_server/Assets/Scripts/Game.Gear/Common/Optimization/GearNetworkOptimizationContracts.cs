using System.Collections.Generic;

namespace Game.Gear.Common
{
    public interface IGearPowerConsumptionProfileProvider
    {
        public bool TryGetGearPowerConsumptionProfile(out GearPowerConsumptionProfile profile);
    }

    public interface IGearConnectCacheProvider
    {
        public void AddGearConnectsTo(List<GearConnect> destination);
    }

    public readonly struct GearPowerConsumptionProfile
    {
        public readonly float MinimumRpm;
        public readonly float BaseRpm;
        public readonly float BaseTorque;
        public readonly float TorqueExponentUnder;
        public readonly float TorqueExponentOver;

        public GearPowerConsumptionProfile(float minimumRpm, float baseRpm, float baseTorque, float torqueExponentUnder, float torqueExponentOver)
        {
            MinimumRpm = minimumRpm;
            BaseRpm = baseRpm;
            BaseTorque = baseTorque;
            TorqueExponentUnder = torqueExponentUnder;
            TorqueExponentOver = torqueExponentOver;
        }

        public bool HasDemand => BaseRpm > 0f && BaseTorque > 0f;
    }
}
