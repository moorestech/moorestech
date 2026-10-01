using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BeltTransport;
using Core.BeltTransport;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Server.Util.MessagePack.BeltTransport;
using Tests.Module.TestMod;
using UnityEngine;
namespace Client.Tests.BeltTransport
{
    public sealed class BeltSurfaceAndOrientationTest
    {
        [TestCase(false, false, BlockDirection.UpNorth)]
        [TestCase(false, true, BlockDirection.UpNorth)]
        [TestCase(true, false, BlockDirection.DownWest)]
        [TestCase(true, true, BlockDirection.DownWest)]
        public void VerticalBeltPreservesPendingInventoryAcrossSaveAndCommittedTicksTest(bool gear, bool legacy, BlockDirection direction)
        {
            var services = CreateWorld();
            var id = gear ? ForUnitTestModBlockId.GearBeltConveyor : ForUnitTestModBlockId.BeltConveyorId;
            var block = Place(id, Vector3Int.zero, direction);
            var belt = block.GetComponent<VanillaBeltConveyorComponent>();
            var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
            belt.SetItem(0, stack);
            var transport = services.GetRequiredService<BeltWorldTransport>();
            Assert.DoesNotThrow(transport.Initialize);
            Assert.IsEmpty(transport.CaptureCommittedSnapshot().Snapshot.Cells);
            Assert.AreEqual(1, belt.InsertItem(stack, InsertItemContext.Empty).Count);
            Assert.IsFalse(belt.InsertionCheck(new List<Core.Item.Interface.IItemStack> { stack }));
            Assert.AreEqual(stack.ItemInstanceId, belt.GetItem(0).ItemInstanceId);
            var world = JArray.FromObject(ServerContext.WorldBlockDatastore.GetSaveJsonObject());
            string key = typeof(VanillaBeltConveyorComponent).FullName;
            if (legacy)
            {
                var savedStack = world[0]["state"][key]["items"][0]["itemStack"].DeepClone();
                world[0]["state"][key] = new JObject { ["legacyItems"] = new JArray(new JObject { ["itemStack"] = savedStack, ["remainingSeconds"] = 0.5 }) };
            }
            var loadedServices = CreateWorld();
            ServerContext.WorldBlockDatastore.LoadBlockDataList(world.ToObject<List<BlockJsonObject>>());
            var loadedTransport = loadedServices.GetRequiredService<BeltWorldTransport>();
            var loaded = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero).GetComponent<VanillaBeltConveyorComponent>();
            Assert.DoesNotThrow(loadedTransport.Initialize);
            for (int tick = 0; tick < 3; tick++) GameUpdater.UpdateOneTick();
            Assert.IsEmpty(loadedTransport.CaptureCommittedSnapshot().Snapshot.Cells);
            Assert.AreEqual(stack.Id, loaded.GetItem(0).Id);
            Assert.AreEqual(1, loaded.GetItem(0).Count);
            if (!legacy) Assert.AreEqual(stack.ItemInstanceId, loaded.GetItem(0).ItemInstanceId);
            Assert.AreEqual(1, ((BeltCellSaveState)loaded.GetSaveState()).Items.Count);
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void SlopeSurfaceFollowsSourceAndTargetHalfCellsAfterWireRoundTripTest(bool gear, bool up)
        {
            var services = CreateWorld();
            var slopeId = gear ? (up ? ForUnitTestModBlockId.TestGearBeltConveyorUp : ForUnitTestModBlockId.TestGearBeltConveyorDown) :
                MasterHolder.BlockMaster.GetBlockId(new Guid(up ? "00000000-0000-0000-0000-0000000000a3" : "00000000-0000-0000-0000-0000000000a4"));
            var flatId = gear ? ForUnitTestModBlockId.GearBeltConveyor : ForUnitTestModBlockId.BeltConveyorId;
            var source = Place(flatId, new Vector3Int(0, up ? 0 : 1, -1), BlockDirection.North);
            var slope = Place(slopeId, Vector3Int.zero, BlockDirection.North);
            var target = Place(flatId, new Vector3Int(0, up ? 1 : 0, 1), BlockDirection.North);
            var transport = services.GetRequiredService<BeltWorldTransport>();
            transport.Initialize();
            var snapshot = BeltWireRoundTripTest.RoundTrip(new BeltSnapshotMessagePack(transport.CaptureCommittedSnapshot())).ToCore().Snapshot;
            Assert.AreEqual(2, snapshot.Connections.Length);
            var cell = snapshot.Cells.Single(value => value.Id == slope.BlockInstanceId.AsPrimitive());
            Assert.AreEqual(up ? 0.1f : 1.1f, cell.Surface.InputHeight);
            Assert.AreEqual(cell.Surface, cell.WithSpeed(32).Surface);
            // p128が共有edgeで、p256とbufferはセル中心面になる。
            // Progress 128 is the shared edge; progress 256 and buffers use the cell center surface.
            AssertHeight(cell.Id, 0, up ? 0.35f : 1.35f, false);
            AssertHeight(cell.Id, 128, up ? 0.45f : 1.45f, false);
            AssertHeight(cell.Id, 256, 0.95f, false);
            AssertHeight(cell.Id, 1, 0.95f, true);
            AssertHeight(target.BlockInstanceId.AsPrimitive(), 0, 0.95f, false);
            AssertHeight(target.BlockInstanceId.AsPrimitive(), 64, up ? 1.20f : 0.70f, false);
            AssertHeight(target.BlockInstanceId.AsPrimitive(), 128, up ? 1.35f : 0.35f, false);
            AssertHeight(target.BlockInstanceId.AsPrimitive(), 256, up ? 1.35f : 0.35f, false);
            // 搬入元を失っても入口面へ保持でき、保存済み原点差を二重加算しない。
            // Missing upstream cells use the entry surface without adding the saved origin difference twice.
            snapshot = new BeltNetworkSnapshot(new[] { cell }, Array.Empty<BeltNetworkConnection>(), Array.Empty<BeltCellItemState>(), Array.Empty<BeltCellPriority>());
            AssertHeight(cell.Id, 64, up ? 0.45f : 1.45f, false);

            #region Internal
            void AssertHeight(int id, int progress, float expected, bool buffer)
            {
                var item = new BeltCellItemState(id, progress, BeltDirection.Back, up ? -1 : 1, new BeltItem(BeltTestState.Identity, 1), buffer);
                var position = BeltItemPosition.Calculate(snapshot, item);
                Assert.That(position.y, Is.EqualTo(expected).Within(0.0001f), $"cell={id}, progress={progress}, buffer={buffer}, position={position}");
                var origin = snapshot.Cells.Single(value => value.Id == id);
                Assert.That(position.z, Is.EqualTo(origin.Z + 0.5f - (buffer ? 0f : 1f - progress / 256f)).Within(0.0001f));
            }
            #endregion
        }
        private static ServiceProvider CreateWorld()
        {
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var container = services.GetRequiredService<MasterJsonFileContainer>();
            var config = container.ConfigJsons[0];
            var file = new JsonFileName("blocks");
            var json = JObject.Parse(config.JsonContents[file]);
            var blocks = (JArray)json["data"];
            var straight = blocks.Single(value => (Guid)value["blockGuid"] == MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockGuid);
            // 通常坂のfixtureは既存歯車坂の接続形状を共有する。
            // Normal slope fixtures share the existing gear slope connection geometry.
            foreach (bool up in new[] { true, false })
            {
                var gearId = up ? ForUnitTestModBlockId.TestGearBeltConveyorUp : ForUnitTestModBlockId.TestGearBeltConveyorDown;
                var gear = blocks.Single(value => (Guid)value["blockGuid"] == MasterHolder.BlockMaster.GetBlockMaster(gearId).BlockGuid);
                var slope = straight.DeepClone();
                slope["blockGuid"] = up ? "00000000-0000-0000-0000-0000000000a3" : "00000000-0000-0000-0000-0000000000a4";
                slope["name"] = up ? "NormalUpSurfaceFixture" : "NormalDownSurfaceFixture";
                slope["blockParam"]["slopeType"] = gear["blockParam"]["slopeType"].DeepClone();
                slope["blockParam"]["inventoryConnectors"] = gear["blockParam"]["inventoryConnectors"].DeepClone();
                blocks.Add(slope);
            }
            var family = json["beltConveyorFamilies"].Single(value => (Guid)value["straightBlockGuid"] == (Guid)straight["blockGuid"]);
            family["upBlockGuid"] = "00000000-0000-0000-0000-0000000000a3";
            family["downBlockGuid"] = "00000000-0000-0000-0000-0000000000a4";
            config.JsonContents[file] = json.ToString();
            MasterHolder.Load(container);
            return services;
        }
        private static IBlock Place(BlockId id, Vector3Int position, BlockDirection direction)
        {
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(id, position, direction, Array.Empty<BlockCreateParam>(), out var block));
            return block;
        }
    }
}
