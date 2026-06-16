using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Core.Update;
using Game.Block.Blocks;
using Game.Context;
using NUnit.Framework;
using UniRx;

namespace Tests.Investigation
{
    public class GameUpdateDispatchOverheadInvestigationTest
    {
        private const int WarmupTicks = 100;
        private const int MeasureTicks = 200;

        [Test]
        public void ProfileCurrentSaveBlockSystemDirectUpdate()
        {
            using var environment = GameUpdatePerformanceTestEnvironment.CreateCurrentSave();
            RunTicks(WarmupTicks);
            var blockSystems = CollectBlockSystems();

            // UniRx を通さず BlockSystem.Update 相当を直接呼ぶ
            // Call the BlockSystem update body directly without UniRx dispatch.
            var samples = MeasureSamples(MeasureTicks, () =>
            {
                for (var i = 0; i < blockSystems.Length; i++)
                    blockSystems[i].DebugUpdateForPerformanceProbe();
            });
            LogStatistics("DirectBlockSystemTick", samples);
        }

        [Test]
        public void ProfileCurrentSaveBlockSystemWrapperOnly()
        {
            using var environment = GameUpdatePerformanceTestEnvironment.CreateCurrentSave();
            RunTicks(WarmupTicks);
            var blockSystems = CollectBlockSystems();

            // component.Update 本体を抜き、BlockSystem の固定費だけを測る
            // Exclude component.Update bodies and measure only BlockSystem wrapper cost.
            var samples = MeasureSamples(MeasureTicks, () =>
            {
                for (var i = 0; i < blockSystems.Length; i++)
                    blockSystems[i].DebugUpdateWithoutComponentBodyForPerformanceProbe();
            });
            LogStatistics("BlockSystemWrapperOnlyTick", samples);
        }

        [Test]
        public void ProfileEmptySubscriberDispatchCost()
        {
            const int subscriberCount = 6302;
            GameUpdater.ResetUpdate();
            var disposables = new List<IDisposable>(subscriberCount);

            // 空 subscriber だけを同数登録し、Subject dispatch の下限を測る
            // Register the same number of empty subscribers to measure dispatch floor cost.
            for (var i = 0; i < subscriberCount; i++)
                disposables.Add(GameUpdater.UpdateObservable.Subscribe(_ => { }));

            var samples = MeasureSamples(MeasureTicks, GameUpdater.Update);
            LogStatistics("EmptySubscriberFullTick", samples);
            UnityEngine.Debug.Log($"[GameUpdateProfile] EmptySubscriberDispatch subscribers={subscriberCount}");

            foreach (var disposable in disposables) disposable.Dispose();
            GameUpdater.Dispose();
        }

        private static BlockSystem[] CollectBlockSystems()
        {
            var blockSystems = ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values
                .Select(data => data.Block)
                .OfType<BlockSystem>()
                .ToArray();
            Assert.That(blockSystems.Length, Is.EqualTo(ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count));
            return blockSystems;
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

        private static void LogStatistics(string name, IReadOnlyList<double> samples)
        {
            var statistics = new GameUpdatePerformanceStatistics(samples);
            UnityEngine.Debug.Log($"[GameUpdateProfile] {name} {statistics.ToLogFields()}");
        }
    }
}
