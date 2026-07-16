using System;
using System.Runtime.CompilerServices;
using Core.Update;
using Game.EnergySystem;
using UnityEngine;

namespace Tests.Util
{
    public static class ElectricConsumerTestUtil
    {
        private static readonly ConditionalWeakTable<IElectricConsumer, PowerOverride> Overrides = new();

        public static void ApplySuppliedPower(IElectricConsumer consumer, float suppliedPower)
        {
            var powerOverride = Overrides.GetValue(consumer, target => new PowerOverride(target));
            powerOverride.SuppliedPower = suppliedPower;
            powerOverride.IsPending = true;

            // 本番の電力確定後かつ通常更新前に、テスト指定の供給量で同じ後処理を上書きする
            // Override through the real post-handler after electric settlement and before normal updates
            if (!GameUpdater.AdditionalUpdates.Contains(powerOverride.Apply))
                GameUpdater.AdditionalUpdates.Add(powerOverride.Apply);
        }

        private sealed class PowerOverride
        {
            public readonly Action Apply;
            public float SuppliedPower;
            public bool IsPending;

            public PowerOverride(IElectricConsumer consumer)
            {
                Apply = () =>
                {
                    if (!IsPending) return;
                    IsPending = false;

                    var requiredPower = consumer.RequestEnergy.AsPrimitive();
                    var powerRate = requiredPower <= 0f ? 1f : Mathf.Clamp01(SuppliedPower / requiredPower);
                    var statistics = new ElectricNetworkStatistics(SuppliedPower, requiredPower, powerRate, 1);
                    ((IElectricTickPostHandler)consumer).OnElectricTickPostProcess(statistics);
                };
            }
        }
    }
}
