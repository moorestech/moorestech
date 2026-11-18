using System;
using System.IO;
using System.Text.RegularExpressions;
using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorLoadFailureSaveLoadTest
    {
        private string _root;
        [TearDown]
        public void Cleanup()
        {
            // silent-ok: 初期化前に失敗した試験には保存先が無い / NUnitが元の失敗を報告する
            if (_root == null || !Directory.Exists(_root)) return;
            Directory.Delete(_root, true);
        }

        [TestCase("shrink")]
        [TestCase("emptyTail")]
        [TestCase("missingConnected")]
        [TestCase("missingSeconds")]
        [TestCase("nullConnected")]
        [TestCase("nullSeconds")]
        [TestCase("validFalseZero")]
        public void DiskLoadKeepsSourceWhenCurrentMasterCannotRetainAllItems(string scenario)
        {
            var save = CreateSaveWithForeignItemInInlet();
            var state = save["world"][0]["state"];
            var requiredKey = scenario.EndsWith("Connected") ? "isConnected" : "remainingClimbSeconds";
            var invalidRequired = scenario.StartsWith("missing") || scenario.StartsWith("null");
            if (invalidRequired)
            {
                var climber = (JObject)state["spaceElevatorClimber"];
                if (scenario.StartsWith("missing")) climber.Remove(requiredKey);
                else climber[requiredKey] = JValue.CreateNull();
            }
            else if (scenario == "shrink" || scenario == "emptyTail")
            {
                var item = scenario == "shrink"
                    ? ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorReward, 3)
                    : ServerContext.ItemStackFactory.CreatEmpty();
                ((JArray)state[typeof(SpaceElevatorBaseInventoryComponent).FullName]).Add(JObject.FromObject(new ItemStackSaveJsonObject(item)));
            }

            var (path, original, loaded) = WriteSaveAndCreateLoader(save);
            if (scenario == "emptyTail" || scenario == "validFalseZero")
            {
                loaded.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
                Assert.AreEqual(1, LoadedInventory().GetItem(0).Count);
            }
            else if (invalidRequired)
            {
                var error = Assert.Throws<Exception>(() => loaded.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize());
                StringAssert.Contains(requiredKey, error.Message);
            }
            else
            {
                LogAssert.Expect(LogType.Error, new Regex("出口縮小.*ロードを中断"));
                var error = Assert.Throws<Exception>(() => loaded.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize());
                StringAssert.Contains("ロード", error.Message);
            }
            Assert.AreEqual(original, File.ReadAllText(path), "failed load overwrote the source save");
        }

        // 昇降を取り消す3つの口すべてで、入口が別の積荷で埋まっていれば出口へ退避してロードを続ける
        // Every cancel path evacuates to the outlet when the inlet holds another item, and the load continues
        [TestCase("unsupported")]
        [TestCase("short")]
        [TestCase("excess")]
        public void DiskLoadEvacuatesReturnedPayloadToOutletWhenInletHoldsAnotherItem(string scenario)
        {
            var save = CreateSaveWithForeignItemInInlet();
            var payloadId = scenario == "unsupported" ? ForUnitTestItemId.ItemId1 : ForUnitTestItemId.TestElevatorBulkPayload;
            var savedCount = scenario switch { "unsupported" => 1, "short" => 2, _ => 5 };
            var returnedCount = scenario == "excess" ? 2 : savedCount;
            save["world"][0]["state"]["spaceElevatorClimber"] = JObject.FromObject(ClimberSave(payloadId, savedCount));

            var (_, _, loaded) = WriteSaveAndCreateLoader(save);
            LogAssert.Expect(LogType.Error, new Regex(scenario switch { "unsupported" => "運ぶ積荷ではない", "short" => "1便の .* 個に足りない", _ => "1便の .* 個だけ昇らせ" }));
            LogAssert.Expect(LogType.Error, new Regex($"入口が別の積荷 .* で埋まっているため.*count:{returnedCount} を出口へ退避"));
            loaded.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();

            // 入口の別の積荷はそのまま、戻した分は出口にあり、1便ぶんだけが昇降中に残る
            // The other item stays in the inlet, the returned part sits in the outlet and only one launch keeps climbing
            var inventory = LoadedInventory();
            Assert.AreEqual(ForUnitTestItemId.ItemId2, inventory.GetItem(0).Id);
            Assert.AreEqual(1, inventory.GetItem(0).Count);
            Assert.AreEqual(payloadId, inventory.GetItem(SpaceElevatorBaseInventoryComponent.FirstOutputSlot).Id);
            Assert.AreEqual(returnedCount, inventory.GetItem(SpaceElevatorBaseInventoryComponent.FirstOutputSlot).Count);
            var refunds = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero).GetComponent<SpaceElevatorClimberComponent>().GetRefundItems();
            Assert.AreEqual(scenario == "excess" ? 3 : 0, refunds.Count == 0 ? 0 : refunds[0].Count);
        }

        [Test]
        public void ReturnedPayloadIsDiscardedWithErrorWhenInletAndOutletAreFull()
        {
            CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            var inventory = Inventory(elevatorBase);
            var inletMax = ItemStackLevelDataStore.Instance.GetMaxStack(ForUnitTestItemId.TestElevatorSatellite);
            var outletMax = ItemStackLevelDataStore.Instance.GetMaxStack(ForUnitTestItemId.TestElevatorReward);
            inventory.SetItem(0, ForUnitTestItemId.TestElevatorSatellite, inletMax);
            inventory.SetItem(SpaceElevatorBaseInventoryComponent.FirstOutputSlot, ForUnitTestItemId.TestElevatorReward, outletMax);

            // 入口も出口も入らないので、ロードを止めずに積荷を消し、消した個数をエラーログへ出す
            // Neither the inlet nor the outlet can take it, so discard the payload without stopping the load and log the count
            LogAssert.Expect(LogType.Error, new Regex("1便の .* 個に足りない"));
            LogAssert.Expect(LogType.Error, new Regex("入口が別の積荷 .* で埋まっているうえ出口も満杯.*count:2 を消します"));
            var climber = LoadClimber(elevatorBase, ClimberSave(ForUnitTestItemId.TestElevatorBulkPayload, 2));
            Assert.AreEqual(inletMax, inventory.GetItem(0).Count);
            Assert.AreEqual(ForUnitTestItemId.TestElevatorSatellite, inventory.GetItem(0).Id);
            Assert.AreEqual(outletMax, inventory.GetItem(SpaceElevatorBaseInventoryComponent.FirstOutputSlot).Count);
            Assert.AreEqual(0, climber.GetRefundItems().Count);
        }

        [Test]
        public void ReturnedPayloadOverflowingSameItemInletGoesToOutlet()
        {
            CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            var inventory = Inventory(elevatorBase);
            var max = ItemStackLevelDataStore.Instance.GetMaxStack(ForUnitTestItemId.TestElevatorBulkPayload);
            inventory.SetItem(0, ForUnitTestItemId.TestElevatorBulkPayload, max - 1);

            // 入口に入る1個だけ入口へ、残り1個は出口へ退避する
            // One fits back into the inlet and the other one is evacuated to the outlet
            LogAssert.Expect(LogType.Error, new Regex("1便の .* 個に足りない"));
            LogAssert.Expect(LogType.Error, new Regex("入口が満杯のため.*count:1 を出口へ退避"));
            LoadClimber(elevatorBase, ClimberSave(ForUnitTestItemId.TestElevatorBulkPayload, 2));
            Assert.AreEqual(max, inventory.GetItem(0).Count);
            Assert.AreEqual(1, inventory.GetItem(SpaceElevatorBaseInventoryComponent.FirstOutputSlot).Count);
        }

        #region Helpers

        private static JObject CreateSaveWithForeignItemInInlet()
        {
            var (_, provider) = CreateServer();
            Inventory(PlaceBase(Vector3Int.zero)).SetItem(0, ForUnitTestItemId.ItemId2, 1);
            return JObject.Parse(provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());
        }

        // 実ファイルへ書き、正規ロード入口を持つ新しいサーバーを作る
        // Write a real file and create a fresh server whose LoadOrInitialize reads it
        private (string Path, string Original, IServiceProvider Loaded) WriteSaveAndCreateLoader(JObject save)
        {
            _root = Path.Combine(Path.GetTempPath(), $"elevator-load-{Guid.NewGuid()}");
            Directory.CreateDirectory(_root);
            var path = WorldDataDirectory.FromWorldRoot(_root).SaveJsonFilePath;
            var original = save.ToString();
            File.WriteAllText(path, original);
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, path),
            };
            var (_, loaded) = new MoorestechServerDIContainerGenerator().Create(options);
            return (path, original, loaded);
        }

        private static SpaceElevatorClimberSaveJsonObject ClimberSave(ItemId payloadId, int count)
        {
            var payload = ServerContext.ItemStackFactory.Create(payloadId, count);
            return new SpaceElevatorClimberSaveJsonObject { IsConnected = true, ClimbingSatellite = new ItemStackSaveJsonObject(payload), RemainingClimbSeconds = 1 };
        }

        private static SpaceElevatorBaseInventoryComponent LoadedInventory()
        {
            return ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero).GetComponent<SpaceElevatorBaseInventoryComponent>();
        }

        #endregion
    }
}
