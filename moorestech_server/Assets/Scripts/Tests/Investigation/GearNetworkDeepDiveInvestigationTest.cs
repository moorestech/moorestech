using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Core.Update;
using Game.Gear.Common;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Tests.Investigation
{
    public class GearNetworkDeepDiveInvestigationTest
    {
        private const int WarmupTicks = 100;
        private const int MeasureTicks = 200;
        private static int _operationSink;
        private static float _floatSink;

        [Test]
        public void ProfileCurrentSaveGearNetworkPhaseBreakdown()
        {
            using var environment = GameUpdatePerformanceTestEnvironment.CreateCurrentSave();
            RunTicks(WarmupTicks);
            var datastore = environment.ServiceProvider.GetService<GearNetworkDatastore>();
            var probe = new GearNetworkManualUpdateProbe();
            var index = 0;

            // 実装本体と replica phase 計測を同じ network 単位で比較する
            // Compare real ManualUpdate and replicated phase timings per network.
            foreach (var network in datastore.GearNetworks.Values)
            {
                var directSamples = MeasureSamples(MeasureTicks, network.ManualUpdate);
                LogStatistics($"GearNetworkDirect index={index} transformers={network.GearTransformers.Count} generators={network.GearGenerators.Count}", directSamples);

                var profiles = new List<GearNetworkManualUpdatePhaseProfile>(MeasureTicks);
                for (var i = 0; i < MeasureTicks; i++) profiles.Add(probe.Run(network));
                LogProfileSummary(index, network, profiles);
                index++;
            }
        }

        [Test]
        public void ProfileCurrentSaveGearNetworkOperationMicroCosts()
        {
            using var environment = GameUpdatePerformanceTestEnvironment.CreateCurrentSave();
            RunTicks(WarmupTicks);
            var datastore = environment.ServiceProvider.GetService<GearNetworkDatastore>();
            var index = 0;

            // phase 内の主要操作だけを単独計測し、巨大 network の内訳を検証する
            // Measure major inner operations directly to validate the large-network phase split.
            foreach (var network in datastore.GearNetworks.Values)
            {
                LogStatistics($"GearNetworkGetConnectsOnly index={index} transformers={network.GearTransformers.Count} generators={network.GearGenerators.Count}", MeasureSamples(MeasureTicks, () => GetAllConnects(network)));
                LogStatistics($"GearNetworkRequiredTorqueOnly index={index} transformers={network.GearTransformers.Count} generators={network.GearGenerators.Count}", MeasureSamples(MeasureTicks, () => GetRequiredTorque(network)));
                LogStatistics($"GearNetworkSupplyPowerOnly index={index} transformers={network.GearTransformers.Count} generators={network.GearGenerators.Count}", MeasureSamples(MeasureTicks, () => SupplyCurrentPower(network)));
                index++;
            }
        }

        private static void LogProfileSummary(int index, GearNetwork network, List<GearNetworkManualUpdatePhaseProfile> profiles)
        {
            var totals = profiles.Select(profile => profile.TotalMilliseconds).ToArray();
            LogStatistics($"GearNetworkProbeTotal index={index} transformers={network.GearTransformers.Count} generators={network.GearGenerators.Count}", totals);

            // phase ごとの分布と巡回量を分け、時間と作業量の対応を見る
            // Split phase distribution and traversal counters to match time with work.
            var phaseNames = profiles.SelectMany(profile => profile.PhaseMilliseconds.Keys).Distinct().OrderBy(name => name);
            foreach (var phaseName in phaseNames)
            {
                var samples = profiles
                    .Select(profile => profile.PhaseMilliseconds.TryGetValue(phaseName, out var value) ? value : 0)
                    .ToArray();
                LogStatistics($"GearNetworkPhase index={index} phase={phaseName}", samples);
            }

            var last = profiles[^1];
            UnityEngine.Debug.Log($"[GameUpdateProfile] GearNetworkPhaseCounters index={index} transformers={network.GearTransformers.Count} generators={network.GearGenerators.Count} {last.ToCounterFields()}");
        }

        private static List<double> MeasureSamples(int count, Action action)
        {
            var samples = new List<double>(count);
            for (var i = 0; i < count; i++) samples.Add(MeasureMilliseconds(action));
            return samples;
        }

        private static void GetAllConnects(GearNetwork network)
        {
            var total = 0;
            _floatSink = 0f;
            foreach (var transformer in network.GearTransformers) total += transformer.GetGearConnects().Count;
            foreach (var generator in network.GearGenerators) total += generator.GetGearConnects().Count;
            _operationSink = total;
        }

        private static void GetRequiredTorque(GearNetwork network)
        {
            var total = 0f;
            _operationSink = 0;
            foreach (var transformer in network.GearTransformers) total += transformer.GetRequiredTorque(transformer.CurrentRpm, transformer.IsCurrentClockwise).AsPrimitive();
            foreach (var generator in network.GearGenerators) total += generator.GetRequiredTorque(generator.CurrentRpm, generator.IsCurrentClockwise).AsPrimitive();
            _floatSink = total;
        }

        private static void SupplyCurrentPower(GearNetwork network)
        {
            _floatSink = 0f;
            foreach (var transformer in network.GearTransformers) transformer.SupplyPower(transformer.CurrentRpm, transformer.CurrentTorque, transformer.IsCurrentClockwise);
            foreach (var generator in network.GearGenerators) generator.SupplyPower(generator.CurrentRpm, generator.CurrentTorque, generator.IsCurrentClockwise);
            _operationSink = network.GearTransformers.Count + network.GearGenerators.Count;
        }

        private static double MeasureMilliseconds(Action action)
        {
            var start = Stopwatch.GetTimestamp();
            action();
            var end = Stopwatch.GetTimestamp();
            return (end - start) * 1000d / Stopwatch.Frequency;
        }

        private static void LogStatistics(string name, IReadOnlyList<double> samples)
        {
            var statistics = new GameUpdatePerformanceStatistics(samples);
            UnityEngine.Debug.Log($"[GameUpdateProfile] {name} {statistics.ToLogFields()} sink={_operationSink} floatSink={_floatSink:F3}");
        }

        private static void RunTicks(int ticks)
        {
            for (var i = 0; i < ticks; i++) GameUpdater.Update();
        }
    }
}
