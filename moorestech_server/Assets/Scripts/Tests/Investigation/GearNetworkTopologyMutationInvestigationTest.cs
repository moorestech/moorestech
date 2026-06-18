using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlockConnectInfoModule;
using NUnit.Framework;

namespace Tests.Investigation
{
    public class GearNetworkTopologyMutationInvestigationTest
    {
        private const int WarmupTicks = 100;
        private const int MutationCount = 200;
        private const int SyntheticGeneratorBaseId = -1900000000;

        [Test]
        public void ProfileCurrentSaveLargestNetworkGeneratorAddMutation()
        {
            using var environment = GameUpdatePerformanceTestEnvironment.CreateCurrentSave();
            RunTicks(WarmupTicks);

            // 最大ネットワークに毎回 generator を追加して topology dirty の負荷を見る
            // Add one generator to the largest network each loop and measure dirty-topology cost.
            var datastore = environment.ServiceProvider.GetService<GearNetworkDatastore>();
            var largestNetwork = GetLargestNetwork(datastore);
            var initialTransformerCount = largestNetwork.GearTransformers.Count;
            var initialGeneratorCount = largestNetwork.GearGenerators.Count;
            var baseGenerator = largestNetwork.GearGenerators.OrderByDescending(generator => generator.GenerateRpm.AsPrimitive()).First();

            var addSamples = new List<double>(MutationCount);
            var updateSamples = new List<double>(MutationCount);
            var combinedSamples = new List<double>(MutationCount);
            var random = new Random(20260619);
            IGearEnergyTransformer attachTarget = baseGenerator;

            for (var i = 0; i < MutationCount; i++)
            {
                // 新 generator を一方向 chain に追加し、rebuild が全 node に到達できるよう root 側へ寄せる
                // Add synthetic generators as a one-way chain and promote them as rebuild roots.
                var generator = CreateSyntheticGenerator(i, random, baseGenerator, attachTarget);
                var addMs = MeasureMilliseconds(() => GearNetworkDatastore.AddGear(generator));
                PromoteGeneratorToTopologyRoot(largestNetwork, generator);

                var updateMs = MeasureMilliseconds(largestNetwork.ManualUpdate);
                addSamples.Add(addMs);
                updateSamples.Add(updateMs);
                combinedSamples.Add(addMs + updateMs);
                attachTarget = generator;
            }

            var finalInfo = largestNetwork.CurrentGearNetworkInfo;
            UnityEngine.Debug.Log($"[GearTopologyMutationProfile] Summary initialTransformers={initialTransformerCount} initialGenerators={initialGeneratorCount} addedGenerators={MutationCount} finalTransformers={largestNetwork.GearTransformers.Count} finalGenerators={largestNetwork.GearGenerators.Count} finalStopReason={finalInfo.StopReason} finalRequiredPower={finalInfo.TotalRequiredGearPower:F3} finalGeneratedPower={finalInfo.TotalGenerateGearPower:F3}");
            LogStatistics("AddGeneratorToLargestNetwork", addSamples);
            LogStatistics("ManualUpdateAfterGeneratorAdd", updateSamples);
            LogStatistics("CombinedAddAndManualUpdate", combinedSamples);
        }

        private static GearNetwork GetLargestNetwork(GearNetworkDatastore datastore)
        {
            return datastore.GearNetworks.Values
                .OrderByDescending(network => network.GearTransformers.Count + network.GearGenerators.Count)
                .First();
        }

        private static SyntheticGearGenerator CreateSyntheticGenerator(int index, Random random, IGearGenerator baseGenerator, IGearEnergyTransformer attachTarget)
        {
            // 最大RPMを変えず、topology rebuild 自体の負荷に寄せる
            // Keep max RPM stable so the measurement focuses on topology rebuild cost.
            var rpm = baseGenerator.GenerateRpm.AsPrimitive();
            var torque = 1f + (float)random.NextDouble();
            var blockInstanceId = new BlockInstanceId(SyntheticGeneratorBaseId + index);
            return new SyntheticGearGenerator(blockInstanceId, rpm, torque, baseGenerator.GenerateIsClockwise, attachTarget);
        }

        private static void PromoteGeneratorToTopologyRoot(GearNetwork network, IGearGenerator generator)
        {
            var field = typeof(GearNetwork).GetField("_gearGenerators", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);

            var generators = (List<IGearGenerator>)field.GetValue(network);
            Assert.That(generators.Remove(generator), Is.True);
            generators.Insert(0, generator);
        }

        private static void LogStatistics(string name, IReadOnlyList<double> samples)
        {
            var statistics = new GameUpdatePerformanceStatistics(samples);
            UnityEngine.Debug.Log($"[GearTopologyMutationProfile] name={name} {statistics.ToLogFields()}");
        }

        private static double MeasureMilliseconds(Action action)
        {
            var start = Stopwatch.GetTimestamp();
            action();
            var end = Stopwatch.GetTimestamp();
            return (end - start) * 1000d / Stopwatch.Frequency;
        }

        private static void RunTicks(int ticks)
        {
            for (var i = 0; i < ticks; i++) GameUpdater.Update();
        }

        private sealed class SyntheticGearGenerator : IGearGenerator
        {
            private readonly List<GearConnect> _connections;

            public SyntheticGearGenerator(BlockInstanceId blockInstanceId, float rpm, float torque, bool isClockwise, IGearEnergyTransformer attachTarget)
            {
                BlockInstanceId = blockInstanceId;
                GenerateRpm = new RPM(rpm);
                GenerateTorque = new Torque(torque);
                GenerateIsClockwise = isClockwise;
                TeethCount = 10;

                // 既存 graph へ同回転でつなぎ、比率 conflict を避ける
                // Connect with the same direction to avoid synthetic ratio conflicts.
                var option = new GearConnectOption(false);
                _connections = new List<GearConnect> { new(attachTarget, option, option) };
            }

            public bool IsDestroy { get; private set; }
            public BlockInstanceId BlockInstanceId { get; }
            public RPM GenerateRpm { get; }
            public Torque GenerateTorque { get; }
            public bool GenerateIsClockwise { get; }
            public int TeethCount { get; }
            public RPM CurrentRpm { get; private set; }
            public Torque CurrentTorque { get; private set; }
            public bool IsCurrentClockwise { get; private set; }

            public void Destroy()
            {
                IsDestroy = true;
            }

            public Torque GetRequiredTorque(RPM rpm, bool isClockwise)
            {
                return new Torque(0);
            }

            public void StopNetwork()
            {
                SupplyPower(new RPM(0), new Torque(0), true);
            }

            public void SupplyPower(RPM rpm, Torque torque, bool isClockwise)
            {
                CurrentRpm = rpm;
                CurrentTorque = torque;
                IsCurrentClockwise = isClockwise;
            }

            public List<GearConnect> GetGearConnects()
            {
                return new List<GearConnect>(_connections);
            }
        }
    }
}
