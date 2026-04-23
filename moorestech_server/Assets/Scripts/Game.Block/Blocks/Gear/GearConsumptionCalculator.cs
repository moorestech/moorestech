using Game.Gear.Common;
using Mooresmaster.Model.GearConsumptionModule;
using UnityEngine;

namespace Game.Block.Blocks.Gear
{
    // RPM比ベースのトルク消費と稼働率を計算する共通式
    // Shared formula for RPM-ratio-based torque consumption and operating rate
    public static class GearConsumptionCalculator
    {
        // マスタ生成型を直接受け取るオーバーロード
        // Overload that accepts the auto-generated master type
        public static Torque CalcRequiredTorque(GearConsumption consumption, RPM currentRpm)
        {
            return CalcRequiredTorque(
                (float)consumption.BaseRpm,
                (float)consumption.MinimumRpm,
                (float)consumption.BaseTorque,
                (float)consumption.TorqueExponentUnder,
                (float)consumption.TorqueExponentOver,
                currentRpm);
        }

        public static float CalcOperatingRate(GearConsumption consumption, RPM currentRpm, Torque currentTorque)
        {
            return CalcOperatingRate(
                (float)consumption.BaseRpm,
                (float)consumption.MinimumRpm,
                (float)consumption.BaseTorque,
                (float)consumption.TorqueExponentUnder,
                (float)consumption.TorqueExponentOver,
                currentRpm, currentTorque);
        }

        // プリミティブ値を直接受け取るオーバーロード（テスト用）
        // Overload that accepts primitive values (for tests)
        public static Torque CalcRequiredTorque(float baseRpm, float minimumRpm, float baseTorque, float expUnder, float expOver, RPM currentRpm)
        {
            if (currentRpm.AsPrimitive() < minimumRpm) return new Torque(0);
            if (baseRpm <= 0f) return new Torque(0);

            var x = currentRpm.AsPrimitive() / baseRpm;
            var exp = x <= 1f ? expUnder : expOver;
            return new Torque(baseTorque * Mathf.Pow(x, exp));
        }

        public static float CalcOperatingRate(float baseRpm, float minimumRpm, float baseTorque, float expUnder, float expOver, RPM currentRpm, Torque currentTorque)
        {
            if (currentRpm.AsPrimitive() < minimumRpm) return 0f;
            if (baseRpm <= 0f) return 0f;

            var rpmRatio = currentRpm.AsPrimitive() / baseRpm;
            var required = CalcRequiredTorque(baseRpm, minimumRpm, baseTorque, expUnder, expOver, currentRpm).AsPrimitive();
            if (required <= 0f) return 0f;

            var torqueRate = Mathf.Min(currentTorque.AsPrimitive() / required, 1f);
            return rpmRatio * torqueRate;
        }
    }
}
