using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Master;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Json.WorldVersions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util.Rocket;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Game.RocketLaunchPad.Regression
{
    public class RocketLaunchPadRetainedItemDiskTest
    {
        private string _root;

        [TearDown]
        public void Cleanup() { if (_root != null && Directory.Exists(_root)) Directory.Delete(_root, true); }

        [TestCase("input")]
        [TestCase("output")]
        [TestCase("cargo")]
        public void MasterChangeRetainsExistingItemsOnDiskLoad(string change)
        {
            _root = Path.Combine(Path.GetTempPath(), $"pad-retained-items-{Guid.NewGuid()}");
            var mod = Path.Combine(_root, "mod");
            foreach (var source in Directory.GetFiles(TestModDirectory.ForUnitTestModDirectory, "*", SearchOption.AllDirectories).Where(path => !path.EndsWith(".meta")))
            {
                var destination = Path.Combine(mod, Path.GetRelativePath(TestModDirectory.ForUnitTestModDirectory, source));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(source, destination);
            }
            var master = Path.Combine(mod, "mods", "forUnitTest", "master");
            var itemsPath = Path.Combine(master, "items.json");
            var items = JObject.Parse(File.ReadAllText(itemsPath));
            foreach (var output in items["orbitalPayloads"].SelectMany(payload => payload["arrivalOutputs"])) output["count"] = 1;
            File.WriteAllText(itemsPath, items.ToString());
            var (_, original) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(mod));

            // 通常の建造と在庫操作から保存
            // Save real loaded cargo and inventory through normal construction and inventory operations
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingBuildCommand, 5));
            pad.GetComponent<RocketLaunchPadProcessorComponent>().RequestBuild();
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingLaunchCommand, 40));
            RocketLaunchPadTestUtil.FillMaterials(pad);
            var layout = RocketLaunchPadTestUtil.GetLayout(pad);
            pad.GetComponent<IOpenableBlockInventoryComponent>().SetItem(layout.OutputSlots[1], ForUnitTestItemId.ComputeDataId, 1);
            var beforeItems = pad.GetComponent<IOpenableBlockInventoryComponent>().InventoryItems.Concat(pad.GetComponent<RocketLaunchPadProcessorComponent>().GetRefundItems())
                .GroupBy(item => item.Id).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
            var text = original.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();
            var directory = WorldDataDirectory.FromWorldRoot(Path.Combine(_root, "world"));
            Directory.CreateDirectory(Path.GetDirectoryName(directory.SaveJsonFilePath));
            File.WriteAllText(directory.SaveJsonFilePath, text);
            var bytes = File.ReadAllBytes(directory.SaveJsonFilePath);

            // 保存には触れず有効なマスタだけ変更
            // Change only valid master settings while preserving the legitimate save
            var blocksPath = Path.Combine(master, "blocks.json");
            var blocks = JObject.Parse(File.ReadAllText(blocksPath));
            var param = blocks["data"].Single(block => (string)block["blockGuid"] == pad.BlockMasterElement.BlockGuid.ToString())["blockParam"];
            if (change == "output") param["outputSlotCount"] = 1;
            else if (change == "input") param["launchMaterials"][0].Remove();
            else
            {
                var guid = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.SatellitePayloadId).ItemGuid.ToString();
                items["orbitalPayloads"].Single(payload => (string)payload["payloadItemGuid"] == guid)["countPerLaunch"] = 2;
                File.WriteAllText(itemsPath, items.ToString());
            }
            File.WriteAllText(blocksPath, blocks.ToString());
            var options = new MoorestechServerDIContainerOptions(mod) { worldDataDirectory = WorldDataDirectory.FromServerDataMap(mod, directory.SaveJsonFilePath) };
            var (_, loaded) = new MoorestechServerDIContainerGenerator().Create(options);
            if (change != "output")
            {
                LogAssert.Expect(LogType.Warning, new Regex("現在の表に合わない"));
                LogAssert.Expect(LogType.Warning, new Regex("スロットへ戻します"));
            }
            loaded.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            var restored = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero);
            var afterItems = restored.GetComponent<IOpenableBlockInventoryComponent>().InventoryItems.Concat(restored.GetComponent<RocketLaunchPadProcessorComponent>().GetRefundItems())
                .GroupBy(item => item.Id).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
            foreach (var pair in beforeItems) Assert.AreEqual(pair.Value, afterItems.GetValueOrDefault(pair.Key));
            Assert.AreEqual(change == "output" ? RocketLaunchPadPhase.AwaitingLaunchCommand : RocketLaunchPadPhase.Idle,
                RocketLaunchPadTestUtil.ReadState(restored.GetComponent<RocketLaunchPadProcessorComponent>()).Phase);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(directory.SaveJsonFilePath));
        }
    }
}
