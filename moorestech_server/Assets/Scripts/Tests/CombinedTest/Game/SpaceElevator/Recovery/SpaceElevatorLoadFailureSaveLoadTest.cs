using System;
using System.IO;
using System.Text.RegularExpressions;
using Core.Item;
using Core.Item.Interface;
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

        [TestCase("unsupported")]
        [TestCase("excess")]
        [TestCase("shrink")]
        [TestCase("emptyTail")]
        [TestCase("missingConnected")]
        [TestCase("missingSeconds")]
        [TestCase("nullConnected")]
        [TestCase("nullSeconds")]
        [TestCase("validFalseZero")]
        public void DiskLoadKeepsSourceWhenCurrentMasterCannotRetainAllItems(string scenario)
        {
            var (_, provider) = CreateServer();
            var block = PlaceBase(Vector3Int.zero);
            var inventory = block.GetComponent<SpaceElevatorBaseInventoryComponent>();
            inventory.SetItem(0, ForUnitTestItemId.ItemId2, 1);
            var save = JObject.Parse(provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());
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
                ((JArray)state[inventory.SaveKey]).Add(JObject.FromObject(new ItemStackSaveJsonObject(item)));
            }
            else if (scenario != "validFalseZero")
            {
                var item = ServerContext.ItemStackFactory.Create(scenario == "unsupported" ? ForUnitTestItemId.ItemId1 : ForUnitTestItemId.TestElevatorSatellite,
                    scenario == "unsupported" ? 1 : 3);
                state["spaceElevatorClimber"] = JObject.FromObject(new SpaceElevatorClimberSaveJsonObject
                {
                    IsConnected = true, ClimbingSatellite = new ItemStackSaveJsonObject(item), RemainingClimbSeconds = 1,
                });
            }

            // 実ファイルから正規のLoadOrInitializeへ渡す
            // Load the actual file through the normal LoadOrInitialize entry point
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
            if (scenario == "emptyTail" || scenario == "validFalseZero")
            {
                loaded.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
                Assert.AreEqual(1, ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero).GetComponent<SpaceElevatorBaseInventoryComponent>().GetItem(0).Count);
            }
            else if (invalidRequired)
            {
                var error = Assert.Throws<Exception>(() => loaded.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize());
                StringAssert.Contains(requiredKey, error.Message);
            }
            else
            {
                if (scenario != "shrink")
                    LogAssert.Expect(LogType.Error, new Regex(scenario == "unsupported" ? "運ぶ積荷ではない" : "1機だけ昇らせ"));
                LogAssert.Expect(LogType.Error, new Regex(scenario == "shrink" ? "出口縮小.*ロードを中断" : "衛星を全量戻せないためロードを中断"));
                var error = Assert.Throws<Exception>(() => loaded.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize());
                StringAssert.Contains("ロード", error.Message);
            }
            Assert.AreEqual(original, File.ReadAllText(path), "failed load overwrote the source save");
        }

        [Test]
        public void PartialReturnRefusalLeavesInletAndSavedSatelliteUntouched()
        {
            CreateServer();
            var inventory = PlaceBase(Vector3Int.zero).GetComponent<SpaceElevatorBaseInventoryComponent>();
            var itemId = ForUnitTestItemId.TestElevatorSatellite;
            var capacity = ItemStackLevelDataStore.Instance.GetMaxStack(itemId);
            inventory.SetItem(0, itemId, capacity - 1);
            var before = inventory.GetItem(0);
            var saved = ServerContext.ItemStackFactory.Create(itemId, 3);
            LogAssert.Expect(LogType.Error, new Regex("衛星を全量戻せないためロードを中断"));
            Assert.IsFalse(inventory.TryReturnSatelliteWithoutEvent(saved));
            Assert.AreSame(before, inventory.GetItem(0));
            Assert.AreEqual(3, saved.Count);
        }
    }
}
