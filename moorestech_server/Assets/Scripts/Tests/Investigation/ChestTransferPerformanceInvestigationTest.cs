using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Connector;
using Game.Context;
using NUnit.Framework;

namespace Tests.Investigation
{
    public class ChestTransferPerformanceInvestigationTest
    {
        private const int WarmupTicks = 100;
        private const int MeasureTicks = 200;

        [Test]
        public void ProfileCurrentSaveChestTransferFullScanVsCurrentUpdate()
        {
            using (GameUpdatePerformanceTestEnvironment.CreateCurrentSave())
            {
                RunTicks(WarmupTicks);
                var fullScanTargets = CollectChestTargets();
                LogChestSummary("FullSlotScan", fullScanTargets);
                var fullScanSamples = MeasureSamples(MeasureTicks, () => UpdateFullSlotScan(fullScanTargets));
                LogStatistics("ChestTransferFullSlotScan", fullScanSamples);
            }

            // 同じセーブを読み直し、測定開始状態をそろえる
            // Reload the same save so both paths start from equivalent state
            using (GameUpdatePerformanceTestEnvironment.CreateCurrentSave())
            {
                RunTicks(WarmupTicks);
                var currentTargets = CollectChestTargets();
                LogChestSummary("CurrentUpdate", currentTargets);
                var currentSamples = MeasureSamples(MeasureTicks, () => UpdateCurrent(currentTargets));
                LogStatistics("ChestTransferCurrentUpdate", currentSamples);
            }
        }

        private static ChestProbeTarget[] CollectChestTargets()
        {
            var inserterField = typeof(VanillaChestComponent).GetField("_blockInventoryInserter", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(inserterField, Is.Not.Null);

            // 現在ワールド内の全チェストと搬出先を収集する
            // Collect all world chests and their output inserters
            return ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values
                .SelectMany(data => data.Block.ComponentManager.GetComponents<VanillaChestComponent>())
                .Select(component => new ChestProbeTarget(component, (IBlockInventoryInserter)inserterField.GetValue(component)))
                .ToArray();
        }

        private static void UpdateFullSlotScan(ChestProbeTarget[] targets)
        {
            for (var targetIndex = 0; targetIndex < targets.Length; targetIndex++)
            {
                var target = targets[targetIndex];

                // 全slotを先頭から走査し、非空slotだけ搬出する
                // Scan every slot from the head and output non-empty stacks
                for (var slot = 0; slot < target.Component.InventoryItems.Count; slot++)
                {
                    var itemStack = target.Component.InventoryItems[slot];
                    if (itemStack.Id == ItemMaster.EmptyItemId || itemStack.Count == 0) continue;
                    var remaining = target.Inserter.InsertItem(itemStack);
                    target.Component.SetItem(slot, remaining);
                }
            }
        }

        private static void UpdateCurrent(ChestProbeTarget[] targets)
        {
            for (var i = 0; i < targets.Length; i++) targets[i].Component.Update();
        }

        private static void LogChestSummary(string label, ChestProbeTarget[] targets)
        {
            var slotCount = 0;
            var nonEmptyCount = 0;

            // セーブ上のチェスト規模をログに残す
            // Log real-save chest scale for comparison with synthetic benchmarks
            for (var i = 0; i < targets.Length; i++)
            {
                var items = targets[i].Component.InventoryItems;
                slotCount += items.Count;
                nonEmptyCount += items.Count(IsNotEmpty);
            }

            UnityEngine.Debug.Log($"[ChestTransferProfile] Summary label={label} chests={targets.Length} slots={slotCount} nonEmptySlots={nonEmptyCount}");
        }

        private static bool IsNotEmpty(IItemStack itemStack)
        {
            return itemStack.Id != ItemMaster.EmptyItemId && itemStack.Count != 0;
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
            UnityEngine.Debug.Log($"[ChestTransferProfile] {name} {statistics.ToLogFields()}");
        }

        private readonly struct ChestProbeTarget
        {
            public VanillaChestComponent Component { get; }
            public IBlockInventoryInserter Inserter { get; }

            public ChestProbeTarget(VanillaChestComponent component, IBlockInventoryInserter inserter)
            {
                Component = component;
                Inserter = inserter;
            }
        }
    }
}
