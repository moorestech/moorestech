using System;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    // 安定tickではgear networkの再計算もtraversal再探索も走らないことを検証するsynthetic benchmark
    // Synthetic benchmark verifying that stable ticks run zero network recalculations and zero traversal rebuilds
    public class GearTickStabilityBenchmarkTest
    {
        [Test]
        public void StableTickProcessesZeroNetworksTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            // 1つの大きめnetwork（generator+shaft+BigGear。過負荷破壊を持たないブロックのみ）を作る
            // Build one larger network (generator + shaft + BigGear; only blocks without overload breakage)
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var shaftBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BigGear, new Vector3Int(-1, -1, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var bigGearBlock);

            // 多数の小network（generator+shaftのペア）を離して並べる
            // Scatter many small networks (generator + shaft pairs) far apart
            const int smallNetworkCount = 10;
            for (var i = 0; i < smallNetworkCount; i++)
            {
                var basePosition = new Vector3Int(100 + i * 10, 0, 0);
                worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, basePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
                worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Shaft, basePosition + new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            }

            var gearTickUpdater = serviceProvider.GetService<GearTickUpdater>();
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();

            // ウォームアップ: 初回tickでtopology適用と全network再計算を走らせる
            // Warm up: the first ticks apply topology and recalculate every network once
            GameUpdater.UpdateOneTick();
            GameUpdater.UpdateOneTick();

            Assert.AreEqual(smallNetworkCount + 1, gearNetworkDatastore.GearNetworks.Count);

            // 安定tickでは処理network数・traversal再構築数がともに0のままであることをassertする
            // Assert both processed-network and traversal-rebuild counts stay zero across stable ticks
            for (var i = 0; i < 20; i++)
            {
                GameUpdater.UpdateOneTick();
                Assert.AreEqual(0, gearTickUpdater.LastTickRecalculatedNetworkCount, $"tick {i} で再計算が走った / recalculation ran on tick {i}");
                Assert.AreEqual(0, gearTickUpdater.LastTickTraversalRebuildCount, $"tick {i} でtraversal再構築が走った / traversal rebuild ran on tick {i}");
            }

            // 安定tick中も供給値は保持され、consumerが読める状態であること
            // Supplied values persist through stable ticks and remain readable by consumers
            var shaft = shaftBlock.GetComponent<GearEnergyTransformer>();
            Assert.AreEqual(10.0f, shaft.CurrentRpm.AsPrimitive(), 0.01f);
            var bigGear = bigGearBlock.GetComponent<GearComponent>();
            Assert.AreEqual(10.0f, bigGear.CurrentRpm.AsPrimitive(), 0.01f);

            // gear追加のあったtickだけ、影響を受けた1networkのみが再計算されること
            // On a tick with a gear addition, only the single affected network is recalculated
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(100, 0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, gearTickUpdater.LastTickRecalculatedNetworkCount);
            Assert.AreEqual(1, gearTickUpdater.LastTickTraversalRebuildCount);

            // その後は再び安定する
            // Afterwards the system settles again
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(0, gearTickUpdater.LastTickRecalculatedNetworkCount);
            Assert.AreEqual(0, gearTickUpdater.LastTickTraversalRebuildCount);
        }
    }
}
