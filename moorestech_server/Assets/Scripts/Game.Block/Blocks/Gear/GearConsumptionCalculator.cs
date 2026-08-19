using Game.EnergySystem;
using Game.Gear.Common;
using Mooresmaster.Model.GearConsumptionModule;
using UnityEngine;

namespace Game.Block.Blocks.Gear
{
    // RPM比ベースのトルク消費と稼働率を計算する共通式
    // Shared formula for RPM-ratio-based torque consumption and operating rate
    public static class GearConsumptionCalculator
    {
        public static Torque CalcRequiredTorque(GearConsumption consumption, RPM currentRpm)
        {
            if (currentRpm.AsPrimitive() < consumption.MinimumRpm) return new Torque(0);
            if (consumption.BaseRpm <= 0f) return new Torque(0);

            var x = currentRpm.AsPrimitive() / consumption.BaseRpm;
            var exp = x <= 1f ? consumption.TorqueExponentUnder : consumption.TorqueExponentOver;
            return new Torque(consumption.BaseTorque * Mathf.Pow(x, exp));
        }

        // 要求トルク率は需要側と同じ上限として効く。1を超える要求も供給・稼働率へ届く
        // The request rate caps the supply the same way it scales demand, so a rate above 1 reaches the supply side too
        public static float CalcOperatingRate(GearConsumption consumption, RPM currentRpm, Torque currentTorque, float requestRate)
        {
            if (currentRpm.AsPrimitive() < consumption.MinimumRpm) return 0f;
            if (consumption.BaseRpm <= 0f) return 0f;

            var rpmRatio = currentRpm.AsPrimitive() / consumption.BaseRpm;
            var required = CalcRequiredTorque(consumption, currentRpm).AsPrimitive();
            if (required <= 0f) return 0f;

            var torqueRate = Mathf.Min(currentTorque.AsPrimitive() / required, requestRate);
            return rpmRatio * torqueRate;
        }

        // 基準電力（baseTorque × baseRpm）に稼働率を乗じた現在の供給電力
        // Base power (baseTorque × baseRpm) scaled by operating rate → current supplied power
        public static ElectricPower CalcCurrentPower(GearConsumption consumption, float operatingRate)
        {
            return new ElectricPower(consumption.BaseTorque * consumption.BaseRpm * operatingRate);
        }
    }
}
