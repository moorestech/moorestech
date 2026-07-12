using System;
using Game.Gear.Common;
using UnityEngine;

namespace Game.Gear.Tick
{
    // networkの需給計算。結果はnetwork単位stateとしてstoreへ書くのみ。各gearの現在値は保持せず導出するため、ここではgearへ書き込まない。
    // Supply-demand calculation for a network. Results are written only as per-network state to the store; per-gear values are derived, not written here.
    public static class GearNetworkPowerCalculator
    {
        public static void CalculateAndDistribute(GearNetwork network, GearNetworkRotationCache cache, GearDemandSnapshot demandSnapshot, GearRuntimeStateStore store)
        {
            // 原点generatorの現在RPMと絶対回転方向。符号付き比をこの2値で実RPM・絶対方向へ展開する
            // The origin generator's current RPM and absolute direction; signed ratios expand into actual RPM/direction via these
            var origin = cache.Origin;
            var originRpm = origin.GenerateRpm.AsPrimitive();
            var originClockwise = origin.GenerateIsClockwise;

            // 各transformerの実RPMと要求トルクから需要動力を集計する（gearへは書かない）
            // Accumulate demand power from each transformer's actual RPM and required torque (nothing is written to gears)
            var demandPower = 0f;
            foreach (var transformer in network.GearTransformers)
            {
                var rotation = cache.GetRotation(transformer.BlockInstanceId);
                var rpm = new RPM(Math.Abs(rotation.SignedRpmRatio) * originRpm);
                var isClockwise = rotation.IsSameDirectionAsOrigin ? originClockwise : !originClockwise;
                var demand = demandSnapshot.GetDemand(transformer.BlockInstanceId);
                if (!demand.DemandEnabled) continue;

                // GetRequiredTorqueは既に要求倍率を含むため、DemandRateを動的化する場合は二重掛けを避けて倍率の責務を片方へ統一する
                // GetRequiredTorque already includes the request rate; if DemandRate becomes dynamic, keep rate ownership in one place to avoid double application
                var requiredTorque = transformer.GetRequiredTorque(rpm, isClockwise).AsPrimitive() * demand.DemandRate;
                demandPower += requiredTorque * rpm.AsPrimitive();
            }

            // 供給可能動力を集計する。燃料切れ等のgeneratorはGenerateRpm/Torqueが0になっており自然に0扱いになる
            // Accumulate available power; starved generators already report zero GenerateRpm/Torque and count as zero
            var availablePower = 0f;
            foreach (var generator in network.GearGenerators)
                availablePower += generator.GenerateTorque.AsPrimitive() * generator.GenerateRpm.AsPrimitive();

            // 需要が供給を上回る場合はblackoutとしてnetworkを停止する
            // When demand exceeds available power the network blacks out and stops
            if (demandPower > availablePower)
            {
                StopNetwork(network, store, GearNetworkStopReason.OverRequirePower, demandPower, availablePower);
                return;
            }

            // 負荷率を確定してnetwork単位stateへ書く。各gearの供給値はこのstateとギア比から導出される
            // Settle the load rate and write the per-network state; each gear's supply is derived from this state and its ratio
            var networkLoadRate = availablePower == 0 ? 0 : Mathf.Min(1, demandPower / availablePower);
            store.SetNetworkState(network.NetworkId, new GearNetworkRuntimeState(false, GearNetworkStopReason.None, demandPower, availablePower, networkLoadRate));
        }

        // 停止理由と需給値をnetwork単位stateへ書く。gearの停止はstateのIsStoppedから導出される
        // Write the stop reason and power figures as per-network state; each gear's stopped-ness is derived from state.IsStopped
        public static void StopNetwork(GearNetwork network, GearRuntimeStateStore store, GearNetworkStopReason stopReason, float demandPower, float availablePower)
        {
            store.SetNetworkState(network.NetworkId, new GearNetworkRuntimeState(true, stopReason, demandPower, availablePower, 0f));
        }
    }
}
