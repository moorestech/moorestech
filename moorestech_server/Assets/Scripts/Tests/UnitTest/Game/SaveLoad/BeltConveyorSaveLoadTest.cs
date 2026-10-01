using System;
using System.Collections.Generic;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.SaveLoad.Migration.Steps;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class BeltConveyorSaveLoadTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void ItemIdentityAndIntegerProgressSurviveSaveLoadTest(bool gear)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var id = gear ? ForUnitTestModBlockId.GearBeltConveyor : ForUnitTestModBlockId.BeltConveyorId;
            var position = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);
            var block = ServerContext.BlockFactory.Create(id, new BlockInstanceId(1), position);
            var belt = block.GetComponent<VanillaBeltConveyorComponent>();
            var item = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1, new ItemInstanceId(-42));
            belt.InsertItem(item, global::Game.Block.Interface.Component.InsertItemContext.Empty);
            var state = SaveLoadJsonTestHelper.ThroughJson(belt.SaveKey, belt.GetSaveState());
            var loaded = ServerContext.BlockFactory.Load(block.BlockGuid, new BlockInstanceId(1), state, position).GetComponent<VanillaBeltConveyorComponent>();

            // タイマーを復元せず、整数位置と個体識別をそのまま保持する。
            // Preserve integer position and identity without reconstructing timers.
            Assert.AreEqual(-42, loaded.GetItem(0).ItemInstanceId.AsPrimitive());
            Assert.AreEqual(item.Id, loaded.GetItem(0).Id);
            Assert.AreEqual(255u, loaded.BeltConveyorItems[0].RemainingTicks);
            Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(belt.GetSaveState()), JToken.FromObject(loaded.GetSaveState())));
        }

        [Test]
        public void LegacyOverlappingSlotsLoadThroughDeterministicRestoreTest()
        {
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId);
            var itemGuid = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).ItemGuid;
            var key = typeof(VanillaBeltConveyorComponent).FullName;
            var slots = new JArray(Legacy(0.25), Legacy(0.5));
            var block = new JObject { ["blockGuid"] = master.BlockGuid.ToString(), ["instanceId"] = 42, ["X"] = 0, ["Y"] = 0, ["Z"] = 0,
                ["direction"] = (int)BlockDirection.North, ["state"] = new JObject { [key] = slots } };
            var save = new JObject { ["worldVersion"] = 3, ["world"] = new JArray(block) };
            Assert.IsTrue(new SaveMigrationStepV3ToV4().Migrate(save).IsConverted);
            ServerContext.WorldBlockDatastore.LoadBlockDataList(save["world"].ToObject<List<BlockJsonObject>>());
            var transport = services.GetRequiredService<BeltWorldTransport>();
            transport.Initialize();
            var items = transport.Network.CaptureItems();
            Assert.AreEqual(1, items.Length);
            Assert.AreEqual(224, items[0].Progress);
            Assert.AreEqual(((long)42 << 32) | 1, BitConverter.ToInt64(items[0].Item.Guid.ToByteArray(), 0));
            var saved = (BeltCellSaveState)ServerContext.WorldBlockDatastore.GetBlock(new BlockInstanceId(42)).GetComponent<VanillaBeltConveyorComponent>().GetSaveState();
            Assert.AreEqual(1, saved.Items.Count);
            Assert.IsNull(saved.BufferItem);

            #region Internal
            string Legacy(double seconds) => new JObject { ["itemStack"] = new JObject { ["itemGuid"] = itemGuid.ToString(), ["count"] = 1 },
                ["remainingSeconds"] = seconds, ["sourceConnectorGuid"] = JValue.CreateNull(), ["goalConnectorGuid"] = JValue.CreateNull() }.ToString(Formatting.None);
            #endregion
        }
    }
}
