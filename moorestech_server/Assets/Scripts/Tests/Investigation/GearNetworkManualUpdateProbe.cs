using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game.Block.Interface;
using Game.Gear.Common;
using UnityEngine;

namespace Tests.Investigation
{
    public class GearNetworkManualUpdateProbe
    {
        public GearNetworkManualUpdatePhaseProfile Run(GearNetwork network)
        {
            var result = new GearNetworkManualUpdatePhaseProfile();
            var totalStart = Stopwatch.GetTimestamp();
            var checkedGearComponents = new Dictionary<BlockInstanceId, GearRotationInfo>(network.GearTransformers.Count + network.GearGenerators.Count);
            IGearGenerator fastestOriginGenerator = null;
            MeasurePhase("SelectGenerator", () =>
            {
                foreach (var gearGenerator in network.GearGenerators)
                    if (fastestOriginGenerator == null || gearGenerator.GenerateRpm > fastestOriginGenerator.GenerateRpm)
                        fastestOriginGenerator = gearGenerator;
            });
            if (fastestOriginGenerator == null)
            {
                MeasurePhase("NoGeneratorStop", () =>
                {
                    foreach (var transformer in network.GearTransformers)
                    { result.StopCalls++; transformer.SupplyPower(new RPM(0), new Torque(0), true); }
                    result.IsStopped = true;
                });
                FinishTotal();
                return result;
            }
            var rocked = false;
            MeasurePhase("PropagateRotation", () =>
            {
                var generatorInfo = CreateRotationInfo(fastestOriginGenerator.GenerateRpm, fastestOriginGenerator.GenerateIsClockwise, fastestOriginGenerator);
                checkedGearComponents.Add(fastestOriginGenerator.BlockInstanceId, generatorInfo);
                foreach (var connect in GetGearConnects(fastestOriginGenerator))
                {
                    rocked = CalcGearInfo(connect, generatorInfo);
                    if (rocked) break;
                }
            });

            if (rocked)
            {
                MeasurePhase("RockedStop", StopNetwork);
                FinishTotal();
                return result;
            }
            (float totalRequiredGearPower, float totalGeneratePower) balance = default;
            MeasurePhase("EnergyBalance", () => { balance = CalculateEnergyBalance(); });
            if (balance.totalRequiredGearPower > balance.totalGeneratePower)
            {
                MeasurePhase("OverRequireStop", StopNetwork);
                FinishTotal();
                return result;
            }
            MeasurePhase("DistributeGearPower", DistributeGearPower);
            FinishTotal();
            return result;
            #region Internal
            bool CalcGearInfo(GearConnect gearConnect, GearRotationInfo connectGearRotationInfo)
            {
                var transformer = gearConnect.Transformer;
                var isReverseRotation = IsReverseRotation(gearConnect);
                var isClockwise = isReverseRotation ? !connectGearRotationInfo.IsClockwise : connectGearRotationInfo.IsClockwise;
                RPM rpm;
                if (transformer is IGear gear &&
                    connectGearRotationInfo.EnergyTransformer is IGear connectGear &&
                    isReverseRotation)
                {
                    var gearRate = (float)connectGear.TeethCount / gear.TeethCount;
                    rpm = connectGearRotationInfo.Rpm * gearRate;
                }
                else
                {
                    rpm = connectGearRotationInfo.Rpm;
                }
                if (checkedGearComponents.TryGetValue(transformer.BlockInstanceId, out var info))
                {
                    if (info.IsClockwise != isClockwise || Math.Abs((info.Rpm - rpm).AsPrimitive()) > 0.1f) return true;
                    return false;
                }
                if (transformer is IGearGenerator generator &&
                    generator.GenerateIsClockwise != isClockwise &&
                    fastestOriginGenerator.BlockInstanceId != transformer.BlockInstanceId)
                    return true;
                var gearRotationInfo = CreateRotationInfo(rpm, isClockwise, transformer);
                checkedGearComponents.Add(transformer.BlockInstanceId, gearRotationInfo);
                foreach (var connect in GetGearConnects(transformer))
                {
                    var isRocked = CalcGearInfo(connect, gearRotationInfo);
                    if (isRocked) return true;
                }
                return false;
            }
            GearRotationInfo CreateRotationInfo(RPM rpm, bool isClockwise, IGearEnergyTransformer transformer)
            {
                result.RequiredTorqueCalls++;
                result.VisitedGearCount++;
                return new GearRotationInfo(rpm, isClockwise, transformer);
            }
            List<GearConnect> GetGearConnects(IGearEnergyTransformer transformer)
            {
                result.GetGearConnectCalls++;
                var connects = transformer.GetGearConnects();
                result.GearConnectEdges += connects.Count;
                return connects;
            }
            static bool IsReverseRotation(GearConnect connect) => connect.Self.IsReverse && connect.Target.IsReverse;
            void StopNetwork()
            {
                foreach (var transformer in network.GearTransformers)
                { result.StopCalls++; transformer.StopNetwork(); }
                foreach (var generator in network.GearGenerators)
                { result.StopCalls++; generator.StopNetwork(); }
                result.IsStopped = true;
            }
            (float totalRequiredGearPower, float totalGeneratePower) CalculateEnergyBalance()
            {
                var totalRequired = 0f;
                foreach (var transformer in network.GearTransformers)
                {
                    result.EnergyBalanceTransformerIterations++;
                    var info = checkedGearComponents[transformer.BlockInstanceId];
                    totalRequired += info.RequiredTorque.AsPrimitive() * info.Rpm.AsPrimitive();
                }
                var totalGenerate = 0f;
                foreach (var generator in network.GearGenerators)
                {
                    result.EnergyBalanceGeneratorIterations++;
                    totalGenerate += generator.GenerateTorque.AsPrimitive() * generator.GenerateRpm.AsPrimitive();
                }
                return (totalRequired, totalGenerate);
            }
            void DistributeGearPower()
            {
                var totalRequiredGearPower = 0f;
                foreach (var transformer in network.GearTransformers)
                {
                    result.DistributeTransformerIterations++;
                    var info = checkedGearComponents[transformer.BlockInstanceId];
                    totalRequiredGearPower += info.RequiredTorque.AsPrimitive() * info.Rpm.AsPrimitive();
                }
                var totalGeneratePower = 0f;
                foreach (var generator in network.GearGenerators)
                {
                    result.DistributeGeneratorIterations++;
                    totalGeneratePower += generator.GenerateTorque.AsPrimitive() * generator.GenerateRpm.AsPrimitive();
                }
                _ = totalGeneratePower == 0 ? 0 : Mathf.Min(1, totalRequiredGearPower / totalGeneratePower);
                foreach (var transformer in network.GearTransformers)
                {
                    var info = checkedGearComponents[transformer.BlockInstanceId];
                    var supplyTorque = info.RequiredTorque / totalRequiredGearPower * totalGeneratePower;
                    if (float.IsNaN(supplyTorque.AsPrimitive())) supplyTorque = new Torque(0);
                    supplyTorque = new Torque(Mathf.Min(supplyTorque.AsPrimitive(), info.RequiredTorque.AsPrimitive()));
                    result.SupplyTransformerCalls++;
                    transformer.SupplyPower(info.Rpm, supplyTorque, info.IsClockwise);
                }
                foreach (var generator in network.GearGenerators)
                {
                    var info = checkedGearComponents[generator.BlockInstanceId];
                    result.SupplyGeneratorCalls++;
                    generator.SupplyPower(info.Rpm, generator.GenerateTorque, info.IsClockwise);
                }
            }
            void MeasurePhase(string phase, Action action)
            {
                var start = Stopwatch.GetTimestamp();
                action();
                var end = Stopwatch.GetTimestamp();
                result.AddPhase(phase, (end - start) * 1000d / Stopwatch.Frequency);
            }
            void FinishTotal()
            {
                var totalEnd = Stopwatch.GetTimestamp();
                result.SetTotalMilliseconds((totalEnd - totalStart) * 1000d / Stopwatch.Frequency);
            }
            #endregion
        }
    }
}
