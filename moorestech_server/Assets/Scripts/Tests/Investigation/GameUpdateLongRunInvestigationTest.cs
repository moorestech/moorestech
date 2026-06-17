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
    public class GameUpdateLongRunInvestigationTest
    {
        private const int WarmupTicks = 500;
        private const int FullTickMeasureTicks = 2000;
        private const int GearNetworkMeasureTicks = 10000;
        private const int FullTickWindowSize = 200;
        private const int GearNetworkWindowSize = 1000;

        [Test]
        public void ProfileCurrentSaveLongRunStableTickDistribution()
        {
            using var environment = GameUpdatePerformanceTestEnvironment.CreateCurrentSave();
            var datastore = environment.ServiceProvider.GetService<GearNetworkDatastore>();
            RunTicks(WarmupTicks);

            // 実tickを長時間流し、定常状態の全体ばらつきを見る
            // Run real ticks for a sustained period and measure whole-tick variance
            var fullTickSamples = new List<double>(FullTickMeasureTicks);
            var postTickGearNetworkSamples = new List<double>(FullTickMeasureTicks);
            for (var i = 0; i < FullTickMeasureTicks; i++)
            {
                fullTickSamples.Add(MeasureMilliseconds(GameUpdater.Update));
                postTickGearNetworkSamples.Add(MeasureMilliseconds(() => UpdateGearNetworks(datastore)));
            }

            LogSeries("LongRunFullTick", fullTickSamples, FullTickWindowSize, new[] { 50d, 75d, 100d });
            LogSeries("LongRunPostTickGearNetworkDatastore", postTickGearNetworkSamples, FullTickWindowSize, new[] { 0.1d, 0.5d, 1d });

            // gear network単体を長く叩き、安定skipの揺らぎだけを分離する
            // Isolate stable-skip variance by repeatedly invoking only the gear network path
            var gearNetworkSamples = MeasureSamples(GearNetworkMeasureTicks, () => UpdateGearNetworks(datastore));
            LogSeries("LongRunGearNetworkDatastore", gearNetworkSamples, GearNetworkWindowSize, new[] { 0.1d, 0.5d, 1d });
            LogLargestNetwork(datastore);
        }

        private static void LogLargestNetwork(GearNetworkDatastore datastore)
        {
            var largest = datastore.GearNetworks.Values
                .OrderByDescending(network => network.GearTransformers.Count + network.GearGenerators.Count)
                .First();

            // 最大networkを単体計測し、巨大componentでもskipが維持されるか確認する
            // Measure the largest network directly to confirm the huge component stays skipped
            var samples = MeasureSamples(GearNetworkMeasureTicks, largest.ManualUpdate);
            var label = $"LongRunLargestGearNetwork transformers={largest.GearTransformers.Count} generators={largest.GearGenerators.Count}";
            LogSeries(label, samples, GearNetworkWindowSize, new[] { 0.05d, 0.1d, 0.5d });
        }

        private static void LogSeries(string name, IReadOnlyList<double> samples, int windowSize, double[] spikeThresholds)
        {
            LogStatistics(name, samples);
            LogWindowStatistics($"{name}Window", samples, windowSize);
            LogSpikeCounts(name, samples, spikeThresholds);
        }

        private static void LogStatistics(string name, IReadOnlyList<double> samples)
        {
            var statistics = new GameUpdatePerformanceStatistics(samples);
            UnityEngine.Debug.Log($"[GameUpdateLongRun] {name} {statistics.ToLogFields()}");
        }

        private static void LogWindowStatistics(string name, IReadOnlyList<double> samples, int windowSize)
        {
            for (var start = 0; start < samples.Count; start += windowSize)
            {
                var window = samples.Skip(start).Take(windowSize).ToArray();
                var statistics = new GameUpdatePerformanceStatistics(window);
                UnityEngine.Debug.Log($"[GameUpdateLongRun] {name} startTick={start} endTick={start + window.Length - 1} {statistics.ToLogFields()}");
            }
        }

        private static void LogSpikeCounts(string name, IReadOnlyList<double> samples, double[] thresholds)
        {
            foreach (var threshold in thresholds)
            {
                var count = samples.Count(sample => sample > threshold);
                UnityEngine.Debug.Log($"[GameUpdateLongRun] {name} spikesAboveMs={threshold:F3} count={count}");
            }
        }

        private static List<double> MeasureSamples(int count, Action action)
        {
            var samples = new List<double>(count);
            for (var i = 0; i < count; i++) samples.Add(MeasureMilliseconds(action));
            return samples;
        }

        private static double MeasureMilliseconds(Action action)
        {
            var start = Stopwatch.GetTimestamp();
            action();
            var end = Stopwatch.GetTimestamp();
            return (end - start) * 1000d / Stopwatch.Frequency;
        }

        private static void UpdateGearNetworks(GearNetworkDatastore datastore)
        {
            foreach (var network in datastore.GearNetworks.Values) network.ManualUpdate();
        }

        private static void RunTicks(int ticks)
        {
            for (var i = 0; i < ticks; i++) GameUpdater.Update();
        }
    }
}
