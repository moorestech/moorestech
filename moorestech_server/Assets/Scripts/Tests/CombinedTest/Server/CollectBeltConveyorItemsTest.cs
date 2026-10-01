using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Entity.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Core.Transport.Segment.BeltWorldTransportTest;

namespace Tests.CombinedTest.Server
{
    public class CollectBeltConveyorItemsTest
    {
        [TestCase(BlockDirection.North, 0, 0.5f, 0.75f)]
        [TestCase(BlockDirection.East, 0, 0.75f, 0.5f)]
        [TestCase(BlockDirection.South, 0, 0.5f, 0.25f)]
        [TestCase(BlockDirection.West, 0, 0.25f, 0.5f)]
        [TestCase(BlockDirection.North, -1, -0.5f, -0.25f)]
        [TestCase(BlockDirection.East, -1, -0.25f, -0.5f)]
        [TestCase(BlockDirection.South, -1, -0.5f, -0.75f)]
        [TestCase(BlockDirection.West, -1, -0.75f, -0.5f)]
        public void BlockDirectionItemPositionTest(BlockDirection direction, int origin, float x, float z)
        {
            var transport = CreateWorld();
            var belt = CreateSavedItem(origin, origin, direction);
            transport.Initialize();
            var entity = Collect()[0];
            Assert.AreEqual(new Vector3(x, CollectBeltConveyorItems.DefaultBeltConveyorHeight, z), entity.Position);
            Assert.AreEqual(100, entity.InstanceId.AsPrimitive());
            Assert.AreEqual(VanillaEntityType.VanillaItem, entity.EntityType);
        }

        [Test]
        public void ItemInstanceIdSurvivesCellBoundaryTest()
        {
            var transport = CreateWorld();
            var first = CreateSavedItem(0, 0, BlockDirection.North);
            var second = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 1, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            transport.Initialize();
            GameUpdater.RunFrames(12);
            Assert.AreEqual(0, first.BeltConveyorItems.Count);
            Assert.AreEqual(100, second.GetItem(0).ItemInstanceId.AsPrimitive());
        }

        [Test]
        public void MasterConnectorIdentityAndProgressAreIncludedTest()
        {
            var transport = CreateWorld();
            var belt = CreateSavedItem(0, 0, BlockDirection.North);
            transport.Initialize();
            var data = MessagePackSerializer.Deserialize<BeltConveyorItemEntityStateMessagePack>(Collect()[0].GetEntityData());
            Assert.AreEqual(belt.BeltConveyorItems[0].StartConnector.ConnectorGuid, data.SourceConnectorGuid);
            Assert.AreEqual(belt.BeltConveyorItems[0].GoalConnector.ConnectorGuid, data.GoalConnectorGuid);
            Assert.AreEqual(0.75f, data.RemainingPercent);
            Assert.AreEqual(0, data.BlockPosX);
            Assert.AreEqual(0, data.BlockPosY);
            Assert.AreEqual(0, data.BlockPosZ);
        }

        private static VanillaBeltConveyorComponent CreateSavedItem(int x, int z, BlockDirection direction)
        {
            var belt = Place(ForUnitTestModBlockId.BeltConveyorId, x, z, direction).GetComponent<VanillaBeltConveyorComponent>();
            // 正規の保存表現で位置を与え、旧配列へのreflectionを不要にする。
            // Seed position through the saved representation instead of reflecting legacy arrays.
            var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1, new ItemInstanceId(100));
            var saved = new BeltCellSaveState { PriorityOrder = 0, Items = new List<BeltCellSavedItem> {
                new BeltCellSavedItem { ItemStack = new ItemStackSaveJsonObject(stack), InstanceId = 100, Progress = 192,
                    EntryDirection = (int)BeltTransportDirections.Opposite(BeltTransportDirections.Forward(belt.Position)), EntryHeight = 0 } } };
            BeltCellSaveCodec.Load(belt, saved, 2);
            return belt;
        }
        private static List<IEntity> Collect() => CollectBeltConveyorItems.CollectItemFromWorld(ServerContext.GetService<IEntityFactory>(), Vector3.zero, float.MaxValue);
    }
}
