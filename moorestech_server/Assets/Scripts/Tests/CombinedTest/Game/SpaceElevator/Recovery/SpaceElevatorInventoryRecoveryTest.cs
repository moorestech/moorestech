using System.Linq;
using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.SpaceElevator;
using MessagePack;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorConnectionTest;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorInventoryRecoveryTest
    {
        [TestCase(0)]
        [TestCase(2)]
        public void SortProtocolPreservesEverySlotRole(int satellites)
        {
            var (_, provider) = CreateServer();
            var inventory = PlaceBase(Vector3Int.zero).GetComponent<SpaceElevatorBaseInventoryComponent>();
            inventory.SetItem(0, ForUnitTestItemId.TestElevatorSatellite, satellites);
            inventory.SetItem(1, ForUnitTestItemId.TestElevatorReward, 5);
            var before = inventory.InventoryItems.ToArray();

            // 整理サービスの直接呼出しではなく受信プロトコルを通す
            // Exercise the receiving protocol rather than calling the sort service directly
            var request = new SortInventoryProtocol.SortInventoryProtocolMessagePack(InventoryIdentifierMessagePack.CreateBlockMessage(Vector3Int.zero));
            new SortInventoryProtocol(provider).GetResponse(MessagePackSerializer.Serialize(request), 1);
            CollectionAssert.AreEqual(before, inventory.InventoryItems);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StackResearchResumesBeforeStartAndAtTopWithoutInventoryMutation(bool atTop)
        {
            CreateServer();
            var block = PlaceBase(Vector3Int.zero);
            PowerBase(block, Vector3Int.zero, 100f);
            LaunchCables(100);
            ServerContext.GetService<SpaceElevatorConnectionService>().Connect(block);
            var inventory = block.GetComponent<SpaceElevatorBaseInventoryComponent>();
            var reward = ForUnitTestItemId.TestElevatorReward;
            var capacity = ItemStackLevelDataStore.Instance.GetMaxStack(reward);
            var guid = MasterHolder.ItemMaster.GetItemMaster(reward).ItemGuid;
            Assert.Less(capacity, MasterHolder.ItemMaster.GetStackLevelTable(MasterHolder.ItemMaster.GetItemMaster(reward).StackLevelTableGuid).StackCounts[1]);
            inventory.SetItem(0, ForUnitTestItemId.TestElevatorSatellite, 1);
            if (atTop) RunSeconds(0.5);
            inventory.SetItem(1, reward, capacity);
            RunSeconds(3);
            Assert.AreEqual(SpaceElevatorBaseStatus.OutputBlocked, ReadDetail(block).Status);
            Assert.AreEqual(atTop ? 1f : 0f, ReadDetail(block).ClimbProgress);

            // 在庫へ触れず研究通知だけで出口待機を解除する
            // Release the outlet wait using only the research notification
            ServerContext.GetService<IItemStackLevelUnlocker>().UnlockStackLevel(guid, 2);
            RunSeconds(2.2);
            Assert.AreEqual(capacity + 5, inventory.GetItem(1).Count);
            Assert.AreEqual(0, inventory.GetItem(0).Count);
            Assert.AreEqual(SpaceElevatorBaseStatus.WaitingSatellite, ReadDetail(block).Status);
        }

        [Test]
        public void UnsupportedInletIsVisibleImmediatelyAndAfterInventoryChange()
        {
            CreateServer();
            var block = PlaceBase(Vector3Int.zero);
            LaunchCables(100);
            ServerContext.GetService<SpaceElevatorConnectionService>().Connect(block);
            var inventory = block.GetComponent<SpaceElevatorBaseInventoryComponent>();
            inventory.SetItem(0, ForUnitTestItemId.ItemId1, 1);
            Assert.AreEqual(SpaceElevatorBaseStatus.InletBlocked, ReadDetail(block).Status);
            RunSeconds(0.1);
            Assert.AreEqual(SpaceElevatorBaseStatus.InletBlocked, ReadDetail(block).Status);
            inventory.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            RunSeconds(0.1);
            Assert.AreEqual(SpaceElevatorBaseStatus.WaitingSatellite, ReadDetail(block).Status);
        }
    }
}
