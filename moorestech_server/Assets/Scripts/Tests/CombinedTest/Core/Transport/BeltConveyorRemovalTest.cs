using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.CombinedTest.Server.PacketTest;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Core.Transport
{
    // 撤去・過負荷破壊でベルコン上のアイテムは返らず消える
    // Items on a belt are neither refunded nor kept when the belt is removed or broken by overload
    public class BeltConveyorRemovalTest : RemoveBlockProtocolTestBase
    {
        private static readonly ItemId ItemA = new(1);

        [Test]
        public void ManualRemovalRefundsNothingAndDropsItemsOnTheRemovedCell()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            var source = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North));
            for (var z = 0; z < 3; z++) Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z), BlockDirection.North);
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ItemA, 3));

            // 出口が無いので3個が0・256・512に詰まり、z=2・z=1・z=0に1個ずつ乗る
            // With no exit the three items pack at 0, 256 and 512, one on each of z=2, z=1 and z=0
            GameUpdater.RunFrames(400);
            Assert.AreEqual(3, ItemsOnSegmentAt(Vector3Int.zero).Length);
            Assert.AreEqual(0, CountOf(source, ItemA));

            // z=1を撤去プロトコルで外す。ベルコンのマスタに必要アイテムは無く、載っていたアイテムも返らない
            // Remove z=1 through the protocol; the belt master requires no items and the carried item is not refunded either
            var response = GetRemoveBlockResponse(packet.GetPacketResponse(RemoveBlock(new Vector3Int(0, 0, 1)), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId)));
            Assert.IsTrue(response.Success);
            for (var i = 0; i < playerInventory.GetSlotSize(); i++) Assert.AreEqual(ItemMaster.EmptyItemId, playerInventory.GetItem(i).Id, $"slot {i}");

            // 次のtickで再構築され、z=1に乗っていたアイテムだけ消える
            // The next tick rebuilds and only the item that sat on z=1 vanishes
            GameUpdater.RunFrames(1);
            Assert.AreEqual(1, ItemsOnSegmentAt(new Vector3Int(0, 0, 2)).Length);
            Assert.AreEqual(1, ItemsOnSegmentAt(Vector3Int.zero).Length);
            Assert.AreEqual(0, CountOf(source, ItemA));
        }

        [Test]
        public void OverloadBreakageDropsItemsOnTheBrokenBelt()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var conveyorPos = Vector3Int.zero;

            // 高速発電機で過負荷になる歯車ベルコンへ、後ろのチェストからアイテムを流す
            // Feed a gear belt that the fast generator overloads, from a chest behind it
            var source = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North));
            world.TryAddBlock(ForUnitTestModBlockId.SimpleFastGearGenerator, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.SmallGearBeltConveyor, conveyorPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ItemA, 5));
            var removalReasons = new List<BlockRemoveReason>();
            using var subscription = ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(update => removalReasons.Add(update.RemoveReason));

            // 1tick目で1個が乗り、以後は壊れるまで回す
            // One item boards on the first tick, then run until the belt breaks
            GameUpdater.RunFrames(1);
            Assert.AreEqual(1, ItemsOnSegmentAt(conveyorPos).Length);
            Assert.AreEqual(4, CountOf(source, ItemA));
            for (var i = 0; i < 200 && world.Exists(conveyorPos); i++) GameUpdater.UpdateOneTick();
            Assert.IsFalse(world.Exists(conveyorPos));
            Assert.IsTrue(removalReasons.Contains(BlockRemoveReason.Broken));

            // 次のtickの再構築でベルコンは無くなり、乗っていた1個はどこにも戻らない
            // The next-tick rebuild has no belt left and the carried item returns nowhere
            GameUpdater.RunFrames(1);
            Assert.IsEmpty(Assembly().Segments);
            Assert.AreEqual(4, CountOf(source, ItemA));
        }
    }
}
