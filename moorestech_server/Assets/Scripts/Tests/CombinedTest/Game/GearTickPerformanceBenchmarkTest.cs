using System;
using System.Diagnostics;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.Gear.Common;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    // 新方式(active-set)と旧方式相当(毎tick全network solve)の実時間を同一ワールドで比較する性能回帰ガード
    // Performance regression guard comparing wall-clock of the active-set path vs the old solve-all-every-tick path on the same world
    public class GearTickPerformanceBenchmarkTest
    {
        [Test]
        public void StableTickIsFasterThanSolvingEveryNetwork()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<GearNetworkDatastore>();
            var tickUpdater = serviceProvider.GetService<GearTickUpdater>();

            // 大きなnetwork（generator＋直列shaft列。過負荷破壊を持たないブロックのみで構成する）
            // One large network (generator + straight shaft line; only blocks without overload breakage)
            const int bigNetworkGearCount = 200;
            world.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            for (var z = 1; z <= bigNetworkGearCount; z++)
                world.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, z), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // 多数の小network（generator＋shaftのペア）を離して並べる
            // Scatter many small networks (generator + shaft pairs) far apart
            const int smallNetworkCount = 50;
            for (var i = 0; i < smallNetworkCount; i++)
            {
                var basePosition = new Vector3Int(100 + i * 10, 0, 0);
                world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, basePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
                world.TryAddBlock(ForUnitTestModBlockId.Shaft, basePosition + new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            }

            // ウォームアップ: topology適用と初回再計算を済ませ、安定状態を確認する
            // Warm up: apply topology and run the initial recalculation, then confirm stability
            for (var i = 0; i < 10; i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(smallNetworkCount + 1, datastore.GearNetworks.Count);
            Assert.AreEqual(0, tickUpdater.LastTickRecalculatedNetworkCount, "ウォームアップ後は安定していること / must be stable after warmup");

            const int measuredTicks = 300;
            var stopwatch = new Stopwatch();

            // 新方式: active-set。安定tickなので実質ゼロ処理
            // New path: active-set. Stable ticks do essentially zero work
            stopwatch.Restart();
            for (var t = 0; t < measuredTicks; t++) tickUpdater.Update();
            var newMs = stopwatch.Elapsed.TotalMilliseconds;

            // 旧方式相当: 毎tick全networkをsolveする（旧GearNetworkDatastore.Updateと同じ計算量）
            // Old-equivalent path: solve every network every tick (same work as the old GearNetworkDatastore.Update)
            stopwatch.Restart();
            for (var t = 0; t < measuredTicks; t++)
                foreach (var network in datastore.GearNetworks.Values)
                    network.ManualUpdate();
            var oldMs = stopwatch.Elapsed.TotalMilliseconds;

            var speedup = newMs > 0.0001 ? oldMs / newMs : double.PositiveInfinity;
            UnityEngine.Debug.Log($"[GearPerf] networks={datastore.GearNetworks.Count} bigNetworkGears={bigNetworkGearCount} ticks={measuredTicks} :: new(active-set)={newMs:F2}ms old(solve-all)={oldMs:F2}ms speedup={speedup:F1}x");

            // 安定tickの新方式は旧方式より確実に速いこと（active-set廃止への回帰ガード）
            // The active-set path on stable ticks must beat solving all networks (guards against reverting active-set)
            Assert.Less(newMs, oldMs, $"new={newMs:F2}ms should be < old={oldMs:F2}ms");
        }
    }
}
