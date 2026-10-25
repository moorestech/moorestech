using System;
using System.IO;
using Core.Update;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.Orbit;
using Game.Paths;
using Game.PlayerIdentity;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Json.WorldVersions;
using Game.SpaceElevator;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util.EnergySystem;
using Tests.Util.PlayerIdentity;
using Tests.Util.Rocket;
using UniRx;
using UnityEngine;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorUpstreamMigrationSaveLoadTest
    {
        private string _root;
        private static readonly Vector3Int PadPosition = new(20, 0, 0);
        private static readonly Vector3Int PolePosition = new(23, 0, 0);

        [TearDown]
        public void Cleanup()
        {
            // silent-ok: 初期化前の失敗には一時ディレクトリが無い / NUnitが元の失敗を報告する
            if (_root == null || !Directory.Exists(_root)) return;
            Directory.Delete(_root, true);
        }

        [TestCase(6, false)]
        [TestCase(7, true)]
        public void DiskMigrationPreservesUpstreamStateAndReloadsConnectedPool(int version, bool hasLaunched)
        {
            var (_, original) = CreateServer();
            PlaceBase(Vector3Int.zero);
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId,
                new Vector3Int(10, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest));
            chest.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ForUnitTestItemId.ItemId2, 7);
            var player = PlayerIdentityTestHelper.Register(original.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;
            original.GetRequiredService<IPlayerInventoryDataStore>().GetInventoryData(player).MainOpenableInventory.SetItem(0, ForUnitTestItemId.ItemId3, 8);
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(PadPosition, 100);
            if (hasLaunched) LaunchCable(pad);

            // 版6は履歴なし、版7は履歴を既に持つ形（部品は素通しする）。どちらもプール節なしの実ファイルにする
            // Write V6 without history and V7 already carrying history (the part passes it through), both without the pool section
            var save = Capture(original);
            save["worldVersion"] = version;
            save.Remove("spaceElevatorCablePool");
            if (version == 6) save.Remove("rocketHasLaunchedOnce");
            else Assert.IsTrue(save["rocketHasLaunchedOnce"].Value<bool>());
            _root = Path.Combine(Path.GetTempPath(), $"elevator-upstream-{Guid.NewGuid()}");
            Directory.CreateDirectory(_root);
            var directory = WorldDataDirectory.FromWorldRoot(_root);
            File.WriteAllText(directory.SaveJsonFilePath, save.ToString());
            var bytes = File.ReadAllBytes(directory.SaveJsonFilePath);
            var loaded = Load(directory.SaveJsonFilePath);
            Assert.AreEqual(0, loaded.GetRequiredService<SpaceElevatorCablePool>().Amount);
            Assert.AreEqual(hasLaunched, loaded.GetRequiredService<RocketLaunchHistory>().HasLaunchedOnce);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(directory.SaveJsonFilePath));
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(loaded.GetRequiredService<WorldDataDirectory>().BackupSaveJsonPath(version)));
            AssertPreserved(save, Capture(loaded));

            // 読込済台で到着・履歴・プール加算を検証
            // Verify arrival, history and pool increments through the loaded launch pad
            pad = ServerContext.WorldBlockDatastore.GetBlock(PadPosition);
            ElectricWireTestUtil.WirePower(PadPosition, PolePosition, 100);
            var arrivals = 0;
            using var subscription = loaded.GetRequiredService<IOrbitalArrivalService>().OnOrbitalArrival.Subscribe(_ => arrivals++);
            for (var i = 0; i < 3; i++) LaunchCable(pad);
            Assert.AreEqual(3, arrivals);
            Assert.IsTrue(loaded.GetRequiredService<RocketLaunchHistory>().HasLaunchedOnce);
            Assert.AreEqual(150, loaded.GetRequiredService<SpaceElevatorCablePool>().Amount);
            var elevator = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero);
            Assert.AreEqual(SpaceElevatorConnectResult.Connected, loaded.GetRequiredService<SpaceElevatorConnectionService>().Connect(elevator));
            Assert.AreEqual(50, loaded.GetRequiredService<SpaceElevatorCablePool>().Amount);

            // 新版も実ファイルへ保存し、新しいDIへのロードで接続と残量を確かめる
            // Save the current version to disk and verify connection and balance in a fresh DI container
            var current = Capture(loaded);
            Assert.AreEqual(WorldSaveAllInfo.CurrentVersion, current["worldVersion"].Value<int>());
            File.WriteAllText(directory.SaveJsonFilePath, current.ToString());
            var restored = Load(directory.SaveJsonFilePath);
            Assert.AreEqual(50, restored.GetRequiredService<SpaceElevatorCablePool>().Amount);
            Assert.IsTrue(restored.GetRequiredService<RocketLaunchHistory>().HasLaunchedOnce);
            Assert.IsTrue(ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero).GetComponent<SpaceElevatorClimberComponent>().IsConnected);
            AssertPreserved(current, Capture(restored));
            TestContext.WriteLine($"C05 version={version} history={hasLaunched}->true backupBytes={bytes.Length} arrivals={arrivals} pool=0->150->50 reloadConnected=true");
        }

        private static ServiceProvider Load(string path)
        {
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, path),
            };
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(options);
            services.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            return services;
        }

        private static JObject Capture(ServiceProvider services)
        {
            return JObject.Parse(services.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());
        }

        private static void AssertPreserved(JObject before, JObject after)
        {
            // ブロック状態は在庫と線の接続記録も含めて全体比較する
            // Compare complete block states, including inventories and wire connection records
            foreach (var key in new[] { "world", "players", "playerInventory", "constructionPayers" })
                Assert.IsTrue(JToken.DeepEquals(before[key], after[key]), $"{key} changed: before={before[key]} after={after[key]}");
        }

        private static void LaunchCable(IBlock pad)
        {
            var pool = ServerContext.GetService<SpaceElevatorCablePool>();
            var expected = pool.Amount + 50;
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.TestElevatorCable, 50);
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            // 合図が必要な初発射と、履歴復元後の自動発射を同じtick入口で進める
            // Advance both the first commanded launch and automatic launches after history restoration through ticks
            for (var i = 0; i < 100 && pool.Amount != expected; i++)
            {
                var phase = RocketLaunchPadTestUtil.ReadState(processor).Phase;
                if (phase == RocketLaunchPadPhase.AwaitingBuildCommand) Assert.AreEqual(RocketLaunchPadCommandResult.Accepted, processor.RequestBuild());
                if (phase == RocketLaunchPadPhase.AwaitingLaunchCommand) Assert.AreEqual(RocketLaunchPadCommandResult.Accepted, processor.RequestLaunch());
                GameUpdater.RunFrames(1);
            }
            Assert.AreEqual(expected, pool.Amount);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Idle, 40));
        }
    }
}
