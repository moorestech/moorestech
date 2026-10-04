using System;
using System.IO;
using System.Text.RegularExpressions;
using Game.Paths;
using UnityEngine.TestTools;
using Game.PlayerIdentity;
using Tests.Util.PlayerIdentity;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Migration.Steps;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using Server.Protocol.PacketResponse.Util.GearChain;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game.SaveLoad
{
    // 版3（種類なし）のセーブが本番と同じ連鎖で版4へ上がり、線種別の固定の種類が埋まってロードできることを検証する
    // Verify a version-3 save (no tool) rises to version 4 through the production chain and loads with the fixed per-kind tools
    public class ConnectToolGuidMigrationLoadTest
    {
        private static readonly Guid WireToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        private static readonly Guid ChainToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000003");

        private string _archiveRoot;

        [SetUp]
        public void SetUp()
        {
            _archiveRoot = SaveLoadPreparerTestFixture.ArchiveRootForThisRun();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_archiveRoot)) Directory.Delete(_archiveRoot, true);
        }

        [Test]
        public void 版3の接続が線種別の種類つきで版4へ移行されロードできる()
        {
            var save = BuildVersion3Save(out var posPole, out var posGenerator, out var posChainA, out var posChainB);

            // 一時ファイルをDIの実セーブパスに指定し、本番の読み込み入口を通す
            // Configure a temporary file as the actual DI save path and exercise the production entry point
            var original = save.ToString();
            Directory.CreateDirectory(_archiveRoot);
            var sourcePath = WorldDataDirectory.FromWorldRoot(_archiveRoot).SaveJsonFilePath;
            File.WriteAllText(sourcePath, original);
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, sourcePath),
            };
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator().Create(options);
            var configuredDirectory = loadProvider.GetRequiredService<WorldDataDirectory>();
            Assert.AreEqual(sourcePath, configuredDirectory.SaveJsonFilePath);
            loadProvider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();

            // ロードは原本を保持し、版3のバックアップと新版で再保存できるワールドを残す
            // Loading preserves the original, archives version 3, and yields a world serializable in the new version
            Assert.AreEqual(original, File.ReadAllText(sourcePath));
            Assert.AreEqual(original, File.ReadAllText(configuredDirectory.BackupSaveJsonPath(3)));
            var loadedSave = JObject.Parse(loadProvider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());
            Assert.AreEqual(4, loadedSave["worldVersion"].Value<int>());

            var pole = ServerContext.WorldBlockDatastore.GetBlock(posPole).GetComponent<IElectricWireConnector>();
            var generator = ServerContext.WorldBlockDatastore.GetBlock(posGenerator).GetComponent<IElectricWireConnector>();
            Assert.AreEqual(SaveMigrationStepV3ToV4.ElectricWireConnectToolGuid, pole.WireConnections[generator.BlockInstanceId].Record.ConnectToolGuid);
            var chainA = ServerContext.WorldBlockDatastore.GetBlock(posChainA).GetComponent<IGearChainPole>();
            var chainB = ServerContext.WorldBlockDatastore.GetBlock(posChainB).GetComponent<IGearChainPole>();
            Assert.IsTrue(chainA.TryGetChainConnectionRecord(chainB.BlockInstanceId, out var chainRecord));
            Assert.AreEqual(SaveMigrationStepV3ToV4.GearChainConnectToolGuid, chainRecord.ConnectToolGuid);

            // 両端の種類と支払い記録が維持される
            // Preserve both endpoints' tool identity and paid material records
            var wireRecord = pole.WireConnections[generator.BlockInstanceId].Record;
            var reverseWire = generator.WireConnections[pole.BlockInstanceId].Record;
            Assert.AreEqual(wireRecord.ConnectToolGuid, reverseWire.ConnectToolGuid);
            Assert.AreEqual(2, wireRecord.TotalCount);
            CollectionAssert.AreEqual(wireRecord.Materials, reverseWire.Materials);
            Assert.IsTrue(chainB.TryGetChainConnectionRecord(chainA.BlockInstanceId, out var reverseChain));
            Assert.AreEqual(chainRecord.ConnectToolGuid, reverseChain.ConnectToolGuid);
            Assert.AreEqual(10, chainRecord.TotalCount);
            CollectionAssert.AreEqual(chainRecord.Materials, reverseChain.Materials);
        }

        [Test]
        public void 壊れた版3は原本を変更せずロードを拒否する()
        {
            var save = BuildVersion3Save(out _, out _, out _, out _);
            // 正常な接続の後に壊れた接続を置き、部分変換後の拒否も確認する
            // Put corruption after valid connections to check refusal after partial conversion
            ((JArray)save["world"]).Add(new JObject
            {
                ["state"] = new JObject { ["GearChainPoleComponent"] = new JObject { ["connections"] = new JArray(1) } }
            });
            var original = save.ToString();
            Directory.CreateDirectory(_archiveRoot);
            var sourcePath = Path.Combine(_archiveRoot, "save.json");
            File.WriteAllText(sourcePath, original);
            var (_, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);
            LogAssert.Expect(LogType.Error, new Regex("変換できませんでした"));
            LogAssert.Expect(LogType.Error, new Regex("^セーブをロードできません"));

            var prepared = preparer.Prepare(File.ReadAllText(sourcePath));

            Assert.IsFalse(prepared.CanLoad);
            StringAssert.Contains("GearChainPoleComponent", prepared.BlockedReason);
            Assert.IsNull(prepared.Save);
            Assert.AreEqual(original, File.ReadAllText(sourcePath));
            Assert.AreEqual(3, save["worldVersion"].Value<int>());
        }

        // 現行の本物のセーブから種類キーを消し版3と名乗らせて、版3の形を作る
        // Strip the tool keys from a real current save and label it version 3 to reproduce the version-3 shape
        private static JObject BuildVersion3Save(out Vector3Int posPole, out Vector3Int posGenerator, out Vector3Int posChainA, out Vector3Int posChainB)
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(provider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;
            var unlock = provider.GetService<IGameUnlockStateDataController>();
            unlock.UnlockConnectTool(WireToolGuid);
            unlock.UnlockConnectTool(ChainToolGuid);
            // 離れた2組を置いて接続種類を分離する
            // Place two separated pairs to isolate the connection kinds
            posPole = new Vector3Int(0, 0, 0);
            posGenerator = new Vector3Int(2, 0, 0);
            posChainA = new Vector3Int(10, 0, 0);
            posChainB = new Vector3Int(12, 0, 0);
            var world = ServerContext.WorldBlockDatastore;
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, posPole, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.GeneratorId, posGenerator, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posChainA, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posChainB, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
            // 本番の接続経路で素材を支払う
            // Pay materials through the production connection paths
            var inventory = provider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000001")), 10));
            inventory.SetItem(1, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000004")), 10));
            Assert.IsTrue(ElectricWireSystemUtil.TryConnect(posPole, posGenerator, playerId, WireToolGuid, out var wireError), wireError.ToString());
            Assert.IsTrue(GearChainSystemUtil.TryConnect(posChainA, posChainB, playerId, ChainToolGuid, out var chainError), chainError.ToString());

            // 保存された4端点だけから種類を消す
            // Strip the tool only from the four saved endpoints
            var save = JObject.Parse(provider.GetService<AssembleSaveJsonText>().AssembleSaveJson());
            var stripped = 0;
            foreach (var block in (JArray)save["world"])
            {
                foreach (var saveKey in new[] { "ElectricWireConnectorComponent", "GearChainPoleComponent" })
                {
                    if (block["state"]?[saveKey]?["connections"] is not JArray connections) continue;
                    foreach (var connection in connections)
                    {
                        Assert.IsTrue(((JObject)connection).Remove("connectToolGuid"));
                        stripped++;
                    }
                }
            }

            Assert.AreEqual(4, stripped, "電線2端＋チェーン2端の種類キーを消せていません");
            save["worldVersion"] = 3;
            return save;
        }
    }
}
