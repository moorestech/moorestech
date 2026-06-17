using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Core.Update;
using Game.Block.Blocks;
using Game.Context;
using NUnit.Framework;

namespace Tests.Investigation
{
    public class BlockSystemDeepDiveInvestigationTest
    {
        private const int WarmupTicks = 100;
        private const int MeasureTicks = 200;
        private const int BlockTypeRepeats = 30;
        private static int _componentCounterSink;

        [Test]
        public void ProfileCurrentSaveBlockSystemInternalCosts()
        {
            using var environment = GameUpdatePerformanceTestEnvironment.CreateCurrentSave();
            RunTicks(WarmupTicks);
            var blockSystems = CollectBlockSystems();
            LogBlockSystemScale(blockSystems);

            // BlockSystem 全体から profiler と component 本体を順に外して固定費を分離する
            // Split BlockSystem cost by removing profiler and component bodies in stages.
            MeasureAndLog("BlockSystemFullWithProfiler", () => UpdateFull(blockSystems));
            MeasureAndLog("BlockSystemNoProfilerComponentBody", () => UpdateWithoutProfiler(blockSystems));
            MeasureAndLog("BlockSystemWrapperProfilerNoBody", () => UpdateWrapperOnly(blockSystems));
            MeasureAndLog("BlockSystemBlockProfilerOnly", () => UpdateBlockProfilerOnly(blockSystems));
            MeasureAndLog("BlockSystemComponentIterationOnly", () => CountComponents(blockSystems));
        }

        [Test]
        public void ProfileCurrentSaveBlockSystemCostByBlockType()
        {
            using var environment = GameUpdatePerformanceTestEnvironment.CreateCurrentSave();
            RunTicks(WarmupTicks);
            var blockSystems = CollectBlockSystems();

            // block type ごとの合計コストを増幅計測し、大きい順にログへ残す
            // Amplify each block type group and log the largest costs first.
            var rows = new List<(string BlockType, int Blocks, int Components, double FullMs, double NoProfilerMs, double WrapperMs)>();
            foreach (var group in blockSystems.GroupBy(block => block.BlockMasterElement.BlockType))
            {
                var blocks = group.ToArray();
                var components = blocks.Sum(block => block.DebugCountComponentsForPerformanceProbe());
                var fullMs = MeasureMilliseconds(() => RepeatBlocks(blocks, BlockTypeRepeats, block => block.DebugUpdateForPerformanceProbe()));
                var noProfilerMs = MeasureMilliseconds(() => RepeatBlocks(blocks, BlockTypeRepeats, block => block.DebugUpdateWithoutProfilerForPerformanceProbe()));
                var wrapperMs = MeasureMilliseconds(() => RepeatBlocks(blocks, BlockTypeRepeats, block => block.DebugUpdateWithoutComponentBodyForPerformanceProbe()));
                rows.Add((group.Key, blocks.Length, components, fullMs, noProfilerMs, wrapperMs));
            }

            foreach (var row in rows.OrderByDescending(row => row.NoProfilerMs).Take(30))
            {
                UnityEngine.Debug.Log($"[GameUpdateProfile] BlockTypeCost blockType={row.BlockType} blocks={row.Blocks} components={row.Components} repeats={BlockTypeRepeats} fullMsPerTick={row.FullMs / BlockTypeRepeats:F3} noProfilerMsPerTick={row.NoProfilerMs / BlockTypeRepeats:F3} wrapperMsPerTick={row.WrapperMs / BlockTypeRepeats:F3}");
            }
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

        private static void LogBlockSystemScale(BlockSystem[] blockSystems)
        {
            var componentCount = blockSystems.Sum(block => block.DebugCountComponentsForPerformanceProbe());
            UnityEngine.Debug.Log($"[GameUpdateProfile] BlockSystemScale blocks={blockSystems.Length} updatableComponents={componentCount}");
        }

        private static void UpdateFull(BlockSystem[] blockSystems)
        {
            for (var i = 0; i < blockSystems.Length; i++) blockSystems[i].DebugUpdateForPerformanceProbe();
        }

        private static void UpdateWithoutProfiler(BlockSystem[] blockSystems)
        {
            for (var i = 0; i < blockSystems.Length; i++) blockSystems[i].DebugUpdateWithoutProfilerForPerformanceProbe();
        }

        private static void UpdateWrapperOnly(BlockSystem[] blockSystems)
        {
            for (var i = 0; i < blockSystems.Length; i++) blockSystems[i].DebugUpdateWithoutComponentBodyForPerformanceProbe();
        }

        private static void UpdateBlockProfilerOnly(BlockSystem[] blockSystems)
        {
            for (var i = 0; i < blockSystems.Length; i++) blockSystems[i].DebugUpdateBlockProfilerOnlyForPerformanceProbe();
        }

        private static void CountComponents(BlockSystem[] blockSystems)
        {
            var total = 0;
            for (var i = 0; i < blockSystems.Length; i++) total += blockSystems[i].DebugCountComponentsForPerformanceProbe();
            _componentCounterSink = total;
        }

        private static void RepeatBlocks(BlockSystem[] blockSystems, int repeats, Action<BlockSystem> action)
        {
            for (var repeat = 0; repeat < repeats; repeat++)
            for (var i = 0; i < blockSystems.Length; i++)
                action(blockSystems[i]);
        }

        private static void MeasureAndLog(string name, Action action)
        {
            var samples = new List<double>(MeasureTicks);
            for (var i = 0; i < MeasureTicks; i++) samples.Add(MeasureMilliseconds(action));
            var statistics = new GameUpdatePerformanceStatistics(samples);
            UnityEngine.Debug.Log($"[GameUpdateProfile] {name} {statistics.ToLogFields()} sink={_componentCounterSink}");
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
    }
}
