using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Core.Update;
using Game.Block.Blocks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Fluid;
using Game.Block.Interface.Component;
using Game.Context;
using NUnit.Framework;
using UniRx;
namespace Tests.Investigation
{
    public class BlockSystemStateAndHotComponentInvestigationTest
    {
        private const int WarmupTicks = 100;
        private const int EventMeasureTicks = 200;
        private const int StateRepeats = 50;
        private const int HotComponentTicks = 10;
        private static readonly FieldInfo FluidPipeBucketsField = typeof(FluidPipeComponent).GetField("_pendingBySource", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo FluidPipeConnectorField = typeof(FluidPipeComponent).GetField("_connectorComponent", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo BeltInserterField = typeof(VanillaBeltConveyorComponent).GetField("_blockInventoryInserter", BindingFlags.Instance | BindingFlags.NonPublic);
        private static int _stateSink;
        [Test]
        public void ProfileCurrentSaveBlockStateEventsAndSerialization()
        {
            using var environment = GameUpdatePerformanceTestEnvironment.CreateCurrentSave();
            RunTicks(WarmupTicks);
            var blockSystems = CollectBlockSystems();
            var eventsByType = SubscribeBlockStateEvents(blockSystems, out var disposables);

            // 実tickで状態通知の発火量を測る
            // Measure state event volume under real tick order.
            RunTicks(EventMeasureTicks);
            foreach (var disposable in disposables) disposable.Dispose();

            LogStateEvents(eventsByType);
            LogGetBlockStateCost(blockSystems);
        }

        [Test]
        public void ProfileCurrentSaveHotComponentShapeAndActualCost()
        {
            using var environment = GameUpdatePerformanceTestEnvironment.CreateCurrentSave();
            RunTicks(WarmupTicks);
            var targets = CollectHotTargets();
            // 重い候補のデータ形状と実tick順コストを対応づける
            // Correlate hot component data shape with actual-order cost.
            LogHotComponentShape(targets);
            var costs = MeasureHotComponentCosts(targets);
            LogHotComponentCosts(costs);
        }

        private static Dictionary<string, EventRow> SubscribeBlockStateEvents(BlockSystem[] blockSystems, out List<IDisposable> disposables)
        {
            var rows = new Dictionary<string, EventRow>();
            disposables = new List<IDisposable>(blockSystems.Length);
            foreach (var blockSystem in blockSystems)
            {
                var blockType = blockSystem.BlockMasterElement.BlockType;
                disposables.Add(blockSystem.BlockStateChange.Subscribe(state => AddEvent(rows, blockType, state.CurrentStateDetails)));
            }
            return rows;
        }

        private static void LogStateEvents(Dictionary<string, EventRow> eventsByType)
        {
            foreach (var pair in eventsByType.OrderByDescending(pair => pair.Value.Count).Take(30))
            {
                var row = pair.Value;
                UnityEngine.Debug.Log($"[GameUpdateProfile] BlockStateEvent blockType={pair.Key} ticks={EventMeasureTicks} count={row.Count} details={row.Details} bytes={row.Bytes} detailsPerEvent={row.Details / (double)row.Count:F3} bytesPerEvent={row.Bytes / (double)row.Count:F3}");
            }
        }

        private static void LogGetBlockStateCost(BlockSystem[] blockSystems)
        {
            var rows = new List<StateCostRow>();
            foreach (var group in blockSystems.GroupBy(block => block.BlockMasterElement.BlockType))
            {
                var blocks = group.ToArray();
                var details = blocks.Sum(block => block.GetBlockState().CurrentStateDetails.Count);
                var bytes = blocks.Sum(block => block.GetBlockState().CurrentStateDetails.Values.Sum(value => value.Length));
                var elapsedMs = MeasureMilliseconds(() => RepeatBlocks(blocks, StateRepeats, block => _stateSink += block.GetBlockState().CurrentStateDetails.Count));
                rows.Add(new StateCostRow(group.Key, blocks.Length, details, bytes, elapsedMs));
            }

            foreach (var row in rows.OrderByDescending(row => row.ElapsedMs).Take(30))
                UnityEngine.Debug.Log($"[GameUpdateProfile] GetBlockStateCost blockType={row.BlockType} blocks={row.Blocks} repeats={StateRepeats} elapsedMs={row.ElapsedMs:F3} msPerSweep={row.ElapsedMs / StateRepeats:F3} usPerBlock={row.ElapsedMs * 1000d / (row.Blocks * StateRepeats):F3} detailsPerBlock={row.Details / (double)row.Blocks:F3} bytesPerBlock={row.Bytes / (double)row.Blocks:F3} sink={_stateSink}");
        }

        private static List<GameUpdateComponentProbeTarget> CollectHotTargets()
        {
            var targets = new List<GameUpdateComponentProbeTarget>();
            foreach (var blockData in ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values)
            foreach (var component in blockData.Block.ComponentManager.GetComponents<IUpdatableBlockComponent>())
                if (component is VanillaChestComponent || component is VanillaBeltConveyorComponent || component is FluidPipeComponent)
                    targets.Add(new GameUpdateComponentProbeTarget(blockData.Block, component));
            return targets;
        }

        private static void LogHotComponentShape(List<GameUpdateComponentProbeTarget> targets)
        {
            var chests = targets.Select(target => target.Component).OfType<VanillaChestComponent>().ToArray();
            var belts = targets.Select(target => target.Component).OfType<VanillaBeltConveyorComponent>().ToArray();
            var pipes = targets.Select(target => target.Component).OfType<FluidPipeComponent>().ToArray();

            // 全体の件数と中身の量を先に固定する
            // Pin down total object counts and payload volume first.
            UnityEngine.Debug.Log($"[GameUpdateProfile] HotShape Chest count={chests.Length} slots={chests.Sum(chest => chest.InventoryItems.Count)} nonEmpty={chests.Sum(CountNonEmptyChestSlots)}");
            UnityEngine.Debug.Log($"[GameUpdateProfile] HotShape Belt count={belts.Length} slots={belts.Sum(belt => belt.BeltConveyorItems.Count)} occupied={belts.Sum(CountOccupiedBeltSlots)} outputReady={belts.Count(belt => belt.BeltConveyorItems.Count > 0 && belt.BeltConveyorItems[0] != null)}");
            UnityEngine.Debug.Log($"[GameUpdateProfile] HotShape FluidPipe count={pipes.Length} active={pipes.Count(pipe => pipe.GetFluidInventory().Count > 0)} buckets={pipes.Sum(CountFluidPipeBuckets)} connections={pipes.Sum(CountFluidPipeConnections)}");
        }

        private static Dictionary<string, CostRow> MeasureHotComponentCosts(List<GameUpdateComponentProbeTarget> targets)
        {
            var rows = new Dictionary<string, CostRow>();
            for (var tick = 0; tick < HotComponentTicks; tick++)
            foreach (var target in targets)
            {
                var key = $"{target.ComponentTypeName} {CreateHotComponentCategory(target.Component)}";
                AddCost(rows, key, MeasureMilliseconds(target.Component.Update));
            }
            return rows;
        }

        private static string CreateHotComponentCategory(IUpdatableBlockComponent component)
        {
            if (component is VanillaChestComponent chest) return $"slots={chest.InventoryItems.Count} nonEmpty={CountNonEmptyChestSlots(chest)}";
            if (component is VanillaBeltConveyorComponent belt) return $"occupied={CountOccupiedBeltSlots(belt)} output={(belt.BeltConveyorItems.Count > 0 && belt.BeltConveyorItems[0] != null ? 1 : 0)} connections={CountBeltConnections(belt)}";
            if (component is FluidPipeComponent pipe) return $"active={pipe.GetFluidInventory().Count} buckets={CountFluidPipeBuckets(pipe)} connections={CountFluidPipeConnections(pipe)}";
            return "unknown";
        }
        private static void LogHotComponentCosts(Dictionary<string, CostRow> rows)
        {
            foreach (var pair in rows.OrderByDescending(pair => pair.Value.ElapsedMs).Take(30))
            {
                var row = pair.Value;
                UnityEngine.Debug.Log($"[GameUpdateProfile] HotComponentCost category=\"{pair.Key}\" calls={row.Calls} elapsedMs={row.ElapsedMs:F3} avgUs={row.ElapsedMs * 1000d / row.Calls:F3}");
            }
        }

        private static int CountNonEmptyChestSlots(VanillaChestComponent chest) => chest.InventoryItems.Count(item => item.Count > 0);
        private static int CountOccupiedBeltSlots(VanillaBeltConveyorComponent belt) => belt.BeltConveyorItems.Count(item => item != null);
        private static int CountBeltConnections(VanillaBeltConveyorComponent belt) => ((IBeltConveyorBlockInventoryInserter)BeltInserterField.GetValue(belt)).ConnectedCount;
        private static int CountFluidPipeBuckets(FluidPipeComponent pipe) => ((IDictionary)FluidPipeBucketsField.GetValue(pipe)).Count;
        private static int CountFluidPipeConnections(FluidPipeComponent pipe) => ((IBlockConnectorComponent<IFluidInventory>)FluidPipeConnectorField.GetValue(pipe)).ConnectedTargets.Count;

        private static BlockSystem[] CollectBlockSystems()
        {
            return ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values.Select(data => data.Block).OfType<BlockSystem>().ToArray();
        }

        private static void AddEvent(Dictionary<string, EventRow> rows, string blockType, Dictionary<string, byte[]> details)
        {
            if (!rows.TryGetValue(blockType, out var row)) row = new EventRow();
            row.Count++;
            row.Details += details.Count;
            row.Bytes += details.Values.Sum(value => value.Length);
            rows[blockType] = row;
        }

        private static void AddCost(Dictionary<string, CostRow> rows, string key, double elapsedMs)
        {
            if (!rows.TryGetValue(key, out var row)) row = new CostRow();
            row.Calls++;
            row.ElapsedMs += elapsedMs;
            rows[key] = row;
        }

        private static void RepeatBlocks(BlockSystem[] blocks, int repeats, Action<BlockSystem> action)
        {
            for (var repeat = 0; repeat < repeats; repeat++)
            for (var i = 0; i < blocks.Length; i++)
                action(blocks[i]);
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

        private struct EventRow { public int Count; public int Details; public int Bytes; }
        private readonly struct StateCostRow { public readonly string BlockType; public readonly int Blocks; public readonly int Details; public readonly int Bytes; public readonly double ElapsedMs; public StateCostRow(string blockType, int blocks, int details, int bytes, double elapsedMs) { BlockType = blockType; Blocks = blocks; Details = details; Bytes = bytes; ElapsedMs = elapsedMs; } }
        private struct CostRow { public int Calls; public double ElapsedMs; }
    }
}
