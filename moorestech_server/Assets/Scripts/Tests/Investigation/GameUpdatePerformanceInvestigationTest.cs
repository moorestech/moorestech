using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Core.Update;
using Game.Block.Interface.Component;
using Game.Context;
using Game.EnergySystem;
using Game.Gear.Common;
using Game.Train.Unit;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using UniRx;

namespace Tests.Investigation
{
    public class GameUpdatePerformanceInvestigationTest
    {
        private const int WarmupTicks = 100;
        private const int MeasureTicks = 300;
        private const int PhaseMeasureTicks = 200;
        private const int WindowSize = 50;
        private const int ActualOrderComponentTicks = 10;
        private const int AmplifiedRepeats = 100;
        [Test]
        public void ProfileCurrentSaveSteadyStateTickDistribution()
        {
            using var environment = GameUpdatePerformanceTestEnvironment.CreateCurrentSave();
            LogWorldSummary(environment.ServiceProvider);
            RunTicks(WarmupTicks);

            // 実際の GameUpdater.Update 全体を連続 tick で測る
            // Measure the full GameUpdater.Update path across sustained ticks.
            var samples = MeasureSamples(MeasureTicks, GameUpdater.Update);
            LogStatistics("FullTick", samples);
            LogWindowStatistics("FullTickWindow", samples, WindowSize);
        }

        [Test]
        public void ProfileCurrentSaveUpdateAndLateUpdatePhases()
        {
            using var environment = GameUpdatePerformanceTestEnvironment.CreateCurrentSave();
            RunTicks(WarmupTicks);
            var updateSubject = GetSubject("_updateSubject");
            var lateUpdateSubject = GetSubject("_lateUpdateSubject");

            // Subject を分けて呼び、Update と LateUpdate の寄与を分離する
            // Invoke each subject separately to split Update and LateUpdate cost.
            var updateSamples = new List<double>(PhaseMeasureTicks);
            var lateSamples = new List<double>(PhaseMeasureTicks);
            for (var i = 0; i < PhaseMeasureTicks; i++)
            {
                updateSamples.Add(MeasureMilliseconds(() => updateSubject.OnNext(Unit.Default)));
                lateSamples.Add(MeasureMilliseconds(() => lateUpdateSubject.OnNext(Unit.Default)));
            }
            LogStatistics("UpdatePhase", updateSamples);
            LogStatistics("LateUpdatePhase", lateSamples);
        }

        [Test]
        public void ProfileCurrentSaveBlockComponentBreakdown()
        {
            using var environment = GameUpdatePerformanceTestEnvironment.CreateCurrentSave();
            RunTicks(WarmupTicks);
            var targets = CollectUpdatableComponents();
            LogComponentCounts(targets);

            // 実 tick 順に呼び、型別に大きい候補を拾う
            // Preserve actual tick order and attribute large candidates by type.
            var actualOrder = MeasureActualOrderComponents(targets, ActualOrderComponentTicks);
            LogComponentDurations("ComponentActualOrder", actualOrder, ActualOrderComponentTicks);

            // 型ごとに反復し、microsecond 級の測定ノイズを平均化する
            // Repeat each type group to average out microsecond-level noise.
            var amplified = MeasureAmplifiedComponents(targets, AmplifiedRepeats);
            LogComponentDurations("ComponentAmplified", amplified, AmplifiedRepeats);
        }

        private static List<GameUpdateComponentProbeTarget> CollectUpdatableComponents()
        {
            var targets = new List<GameUpdateComponentProbeTarget>();
            foreach (var blockData in ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values)
            {
                foreach (var component in blockData.Block.ComponentManager.GetComponents<IUpdatableBlockComponent>())
                {
                    targets.Add(new GameUpdateComponentProbeTarget(blockData.Block, component));
                }
            }
            return targets;
        }

        private static Dictionary<string, (int Count, double ElapsedMs)> MeasureActualOrderComponents(
            List<GameUpdateComponentProbeTarget> targets,
            int ticks)
        {
            var result = new Dictionary<string, (int Count, double ElapsedMs)>();
            for (var tick = 0; tick < ticks; tick++)
            {
                foreach (var target in targets)
                {
                    var elapsedMs = MeasureMilliseconds(target.Component.Update);
                    AddDuration(result, target.ComponentTypeName, 1, elapsedMs);
                }
            }
            return result;
        }

        private static Dictionary<string, (int Count, double ElapsedMs)> MeasureAmplifiedComponents(
            List<GameUpdateComponentProbeTarget> targets,
            int repeats)
        {
            var result = new Dictionary<string, (int Count, double ElapsedMs)>();
            foreach (var group in targets.GroupBy(target => target.ComponentTypeName))
            {
                var components = group.Select(target => target.Component).ToArray();
                var elapsedMs = MeasureMilliseconds(() =>
                {
                    for (var repeat = 0; repeat < repeats; repeat++)
                    for (var i = 0; i < components.Length; i++)
                        components[i].Update();
                });
                AddDuration(result, group.Key, components.Length * repeats, elapsedMs);
            }
            return result;
        }

        private static void LogWorldSummary(ServiceProvider serviceProvider)
        {
            var blockCount = ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count;
            var components = CollectUpdatableComponents();
            var energySegments = serviceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>().GetEnergySegmentListCount();
            var gearNetworks = serviceProvider.GetService<GearNetworkDatastore>().GearNetworks.Count;
            var trains = serviceProvider.GetService<ITrainUnitLookupDatastore>().GetRegisteredTrains().Count;
            UnityEngine.Debug.Log($"[GameUpdateProfile] Summary blocks={blockCount} updatableComponents={components.Count} energySegments={energySegments} gearNetworks={gearNetworks} trains={trains}");
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

        private static void RunTicks(int ticks)
        {
            for (var i = 0; i < ticks; i++) GameUpdater.Update();
        }

        private static Subject<Unit> GetSubject(string fieldName)
        {
            var field = typeof(GameUpdater).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (Subject<Unit>)field.GetValue(null);
        }

        private static void AddDuration(Dictionary<string, (int Count, double ElapsedMs)> result, string key, int count, double elapsedMs)
        {
            if (!result.TryGetValue(key, out var current)) current = (0, 0);
            result[key] = (current.Count + count, current.ElapsedMs + elapsedMs);
        }

        private static void LogStatistics(string name, IReadOnlyList<double> samples)
        {
            var statistics = new GameUpdatePerformanceStatistics(samples);
            UnityEngine.Debug.Log($"[GameUpdateProfile] {name} {statistics.ToLogFields()}");
        }

        private static void LogWindowStatistics(string name, IReadOnlyList<double> samples, int windowSize)
        {
            for (var start = 0; start < samples.Count; start += windowSize)
            {
                var window = samples.Skip(start).Take(windowSize).ToArray();
                var statistics = new GameUpdatePerformanceStatistics(window);
                UnityEngine.Debug.Log($"[GameUpdateProfile] {name} startTick={start} endTick={start + window.Length - 1} {statistics.ToLogFields()}");
            }
        }

        private static void LogComponentCounts(List<GameUpdateComponentProbeTarget> targets)
        {
            foreach (var group in targets.GroupBy(target => target.ComponentTypeName).OrderByDescending(group => group.Count()))
                UnityEngine.Debug.Log($"[GameUpdateProfile] ComponentCount type={group.Key} count={group.Count()}");
        }
        private static void LogComponentDurations(string label, Dictionary<string, (int Count, double ElapsedMs)> durations, int repeats)
        {
            foreach (var pair in durations.OrderByDescending(pair => pair.Value.ElapsedMs).Take(20))
            {
                var avgUs = pair.Value.ElapsedMs * 1000d / pair.Value.Count;
                UnityEngine.Debug.Log($"[GameUpdateProfile] {label} type={pair.Key} calls={pair.Value.Count} repeats={repeats} elapsedMs={pair.Value.ElapsedMs:F3} avgUs={avgUs:F3}");
            }
        }
    }
}
