using System.IO;
using System.Linq;
using System.Reflection;
using Core.Inventory;
using System.Collections.Generic;
using UniRx;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.Orbit;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util.Rocket;
using UnityEngine;

namespace Tests.CombinedTest.Game.RocketLaunchPad.Regression
{
    public class RocketLaunchPadDuplicateOutputTest
    {
        private string _variantRoot;

        [TearDown]
        public void CleanupVariant()
        {
            // silent-ok: 通常modの試験には独立コピーが無い / NUnitが各試験の結果を報告する
            if (_variantRoot == null || !Directory.Exists(_variantRoot)) return;
            Directory.Delete(_variantRoot, true);
            _variantRoot = null;
        }

        [TestCase(int.MaxValue, 1)]
        [TestCase(1500000000, 1500000000)]
        [TestCase(int.MaxValue, int.MaxValue)]
        public void 巨大な同種出力の容量検査と格納で総数を保存する(int existingCount, int arrivalCount)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemId = ForUnitTestItemId.ComputeDataId;
            var master = MasterHolder.ItemMaster.GetItemMaster(itemId);
            var counts = MasterHolder.ItemMaster.GetStackLevelTable(master.StackLevelTableGuid).StackCounts;
            var originalLimit = counts[0];
            counts[0] = int.MaxValue;
            try
            {
                // 実発射台の出力担当を使い、容量検査が実在庫を変えないことも確かめる
                // Use the real pad output owner and verify its fit check leaves inventory untouched
                var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
                var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
                inventory.SetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[1], itemId, existingCount);
                var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
                var arrival = new IItemStack[] { ServerContext.ItemStackFactory.Create(itemId, arrivalCount) };
                var outputSlots = CreateOutputSlots(pad, processor, arrival);
                Assert.IsTrue(outputSlots.CanAcceptArrival(ForUnitTestItemId.SatellitePayloadId, 1));
                Assert.AreEqual(0, inventory.GetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[0]).Count);
                Assert.AreEqual(existingCount, inventory.GetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[1]).Count);

                // 同種枠を先に満たし、intを超える総数をlongで比較する
                // Fill the matching slot first and compare totals above int range using long
                Assert.IsTrue(outputSlots.TryCompleteArrival(ForUnitTestItemId.SatellitePayloadId, 1, out _));
                var expectedTotal = (long)existingCount + arrivalCount;
                var actualTotal = (long)inventory.GetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[0]).Count + inventory.GetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[1]).Count;
                TestContext.WriteLine($"existing={existingCount}; arrival={arrivalCount}; slots=[{inventory.GetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[0]).Count},{inventory.GetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[1]).Count}]; total={actualTotal}; expected={expectedTotal}");
                Assert.AreEqual(expectedTotal, actualTotal);
                Assert.AreEqual(int.MaxValue, inventory.GetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[1]).Count);
                Assert.AreEqual(expectedTotal - int.MaxValue, inventory.GetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[0]).Count);
                Assert.AreEqual(itemId, inventory.GetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[0]).Id);
                Assert.AreEqual(itemId, inventory.GetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[1]).Id);
            }
            finally
            {
                // テストが変更したメモリ内上限を失敗時も復元する
                // Restore the in-memory stack limit even when the regression fails
                counts[0] = originalLimit;
            }
        }

        [Test]
        public void 発射台は近傍順へ変わらず先頭の空枠へ溢れる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var configs = ModJsonStringLoader.GetMasterString(new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods")));
            var key = new JsonFileName("blocks");
            var blocks = JObject.Parse(configs[0].JsonContents[key]);
            blocks["data"].Single(block => (string)block["name"] == "TestRocketLaunchPad")["blockParam"]["outputSlotCount"] = 3;
            configs[0].JsonContents[key] = blocks.ToString();
            _variantRoot = RocketLaunchPadVariantTestMod.CreateServer(configs[0].JsonContents[new JsonFileName("items")], configs[0].JsonContents[new JsonFileName("blocks")]);
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            inventory.SetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[2], ForUnitTestItemId.ComputeDataId, 99);
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            var arrival = new[] { ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ComputeDataId, 2) };

            // 通常在庫の近傍順[空,A1,A100]とは違う先頭順を保持する
            // Preserve first-empty order instead of the normal inventory's proximity order [empty,A1,A100]
            var changed = new List<int>();
            using var subscription = ServerContext.BlockOpenableInventoryUpdateEvent.OnInventoryUpdated
                .Where(change => change.BlockInstanceId == pad.BlockInstanceId).Subscribe(change => changed.Add(change.Slot));
            var output = CreateOutputSlots(pad, processor, arrival);
            Assert.IsTrue(output.CanAcceptArrival(ForUnitTestItemId.SatellitePayloadId, 1));
            CollectionAssert.AreEqual(new[] { 0, 0, 99 }, inventory.InventoryItems.Skip(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlotStart).Select(item => item.Count));
            Assert.IsTrue(output.TryCompleteArrival(ForUnitTestItemId.SatellitePayloadId, 1, out _));
            CollectionAssert.AreEqual(new[] { 1, 0, 100 }, inventory.InventoryItems.Skip(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlotStart).Select(item => item.Count));
            CollectionAssert.AreEqual(new[] { RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[0], RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[2] }, changed);
        }

        private static RocketLaunchPadOutputSlots CreateOutputSlots(global::Game.Block.Interface.IBlock pad, RocketLaunchPadProcessorComponent processor, IItemStack[] outputs)
        {
            var slots = (OpenableInventoryItemDataStoreService)typeof(RocketLaunchPadProcessorComponent)
                .GetField("_slots", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(processor);
            return new RocketLaunchPadOutputSlots(RocketLaunchPadTestUtil.GetLayout(pad), slots,
                new global::Game.Block.Blocks.Service.ConnectingInventoryListPriorityInsertItemService(pad.BlockInstanceId,
                    global::Game.Block.Factory.BlockTemplate.BlockTemplateUtil.CreateInventoryConnector(
                        ((Mooresmaster.Model.BlocksModule.RocketLaunchPadBlockParam)pad.BlockMasterElement.BlockParam).InventoryConnectors, pad.BlockPositionInfo)),
                ServerContext.GetService<global::Game.SpaceElevator.SpaceElevatorCablePool>(), new FixedOutputArrivalService(outputs));
        }

        [Test]
        public void 同一成果二行を一枠に合算して検証し格納する()
        {
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var configs = ModJsonStringLoader.GetMasterString(new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods")));
            var itemsName = new JsonFileName("items");
            var items = JObject.Parse(configs[0].JsonContents[itemsName]);
            var outputs = (JArray)items["orbitalPayloads"][0]["arrivalOutputs"];
            outputs[0]["count"] = 40;
            outputs.Add(outputs[0].DeepClone());
            configs[0].JsonContents[itemsName] = items.ToString();
            var blocksName = new JsonFileName("blocks");
            var blocks = JObject.Parse(configs[0].JsonContents[blocksName]);
            blocks["data"].Single(block => (string)block["name"] == "TestRocketLaunchPad")["blockParam"]["outputSlotCount"] = 1;
            configs[0].JsonContents[blocksName] = blocks.ToString();

            // 実ローダーの容量検証と実発射を同じマスタ入力で通す
            // Exercise the real capacity validator and actual launch with the same master input
            _variantRoot = RocketLaunchPadVariantTestMod.CreateServer(configs[0].JsonContents[itemsName], configs[0].JsonContents[blocksName]);
            ServerContext.GetService<RocketLaunchHistory>().RecordLaunch();
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Launching, 40));
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            Assert.AreEqual(RocketLaunchPadTestUtil.GetLayout(pad).TotalSlotCount, inventory.GetSlotSize());
            Assert.AreEqual(80, inventory.GetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[0]).Count);
        }
        [Test]
        public void 空枠より同種の既存出力を先に埋め別成果も格納する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var configs = ModJsonStringLoader.GetMasterString(new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods")));
            var itemsName = new JsonFileName("items");
            var items = JObject.Parse(configs[0].JsonContents[itemsName]);
            var outputs = (JArray)items["orbitalPayloads"][0]["arrivalOutputs"];
            outputs[0]["count"] = 50;
            var second = (JObject)outputs[0].DeepClone();
            second["itemGuid"] = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.RocketFuelId).ItemGuid.ToString();
            outputs.Add(second);
            configs[0].JsonContents[itemsName] = items.ToString();
            _variantRoot = RocketLaunchPadVariantTestMod.CreateServer(configs[0].JsonContents[new JsonFileName("items")], configs[0].JsonContents[new JsonFileName("blocks")]);

            // 出力[空,A50]へ成果[A50,B50]を実際の建造・発射で格納する
            // Store results [A50,B50] into [empty,A50] through the real build and launch
            ServerContext.GetService<RocketLaunchHistory>().RecordLaunch();
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            inventory.SetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[1], ForUnitTestItemId.ComputeDataId, 50);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Launching, 40));
            Assert.AreEqual(ForUnitTestItemId.RocketFuelId, inventory.GetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[0]).Id);
            Assert.AreEqual(50, inventory.GetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[0]).Count);
            Assert.AreEqual(ForUnitTestItemId.ComputeDataId, inventory.GetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[1]).Id);
            Assert.AreEqual(100, inventory.GetItem(RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots[1]).Count);
        }
    }
}
