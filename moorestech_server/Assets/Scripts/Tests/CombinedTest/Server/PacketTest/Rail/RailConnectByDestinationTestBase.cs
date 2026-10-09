using Server.Protocol.PacketResponse.Rail;
using System;
using System.Linq;
using Core.Inventory;
using Core.Item;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.RailGraph;
using Game.Train.SaveLoad;
using Game.PlayerIdentity;
using Server.Event.Notification;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Util.PlayerIdentity;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest.Rail
{
    public abstract class RailConnectByDestinationTestBase
    {
        protected int PlayerId;
        protected static readonly Guid ConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000002");
        protected static readonly Guid ReinforcingMaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");
        protected static readonly Guid IronPlateGuid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        protected static readonly Vector3Int ToRailPosition = new(10, 0, 0);

        protected TrainTestEnvironment _environment;
        protected IOpenableInventory _inventory;
        protected RailNode _fromNode;
        protected RailNode _toNode;
        protected ItemId _reinforcingMaterialId;
        protected ItemId _ironPlateId;

        private CapturedEventSink _requesterSink;
        private CapturedEventSink _otherSink;

        [SetUp]
        public void SetUp()
        {
            // レール端点と解放済みツールを準備
            // Prepare rail endpoints and the unlocked connectTool
            _environment = TrainTestHelper.CreateEnvironment();
            var registry = _environment.ServiceProvider.GetRequiredService<PlayerIdentityRegistry>();
            PlayerId = PlayerIdentityTestHelper.Register(registry, "steam:1").PlayerId;
            var otherId = PlayerIdentityTestHelper.Register(registry, "steam:2").PlayerId;
            _requesterSink = EventTestUtil.RegisterCaptureSink(_environment.ServiceProvider, PlayerId);
            _otherSink = EventTestUtil.RegisterCaptureSink(_environment.ServiceProvider, otherId);

            // 配置済み端点とインベントリを用意し、準備中の通知を捨てる
            // Prepare placed endpoints and inventory, then discard setup notifications
            _fromNode = TrainTestHelper.PlaceRail(_environment, Vector3Int.zero, BlockDirection.North).FrontNode;
            _toNode = TrainTestHelper.PlaceRail(_environment, ToRailPosition, BlockDirection.North).BackNode;
            _inventory = _environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            _environment.ServiceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(ConnectToolGuid);
            _reinforcingMaterialId = MasterHolder.ItemMaster.GetItemId(ReinforcingMaterialGuid);
            _ironPlateId = MasterHolder.ItemMaster.GetItemId(IronPlateGuid);
            _requesterSink.TakeAll();
            _otherSink.TakeAll();
        }

        protected int CalculateUnits(RailNode from, RailNode to)
        {
            var length = RailConnectionEditProtocol.GetRailLength(from, to);
            return Mathf.CeilToInt(length / 5f);
        }

        protected void SetInventory(int reinforcingCount, int ironPlateCount)
        {
            // 2本分の素材も上限を守って複数枠へ分配
            // Distribute even a two-rail budget across slots within each stack limit
            var slot = 0;
            AddStacks(_reinforcingMaterialId, reinforcingCount);
            AddStacks(_ironPlateId, ironPlateCount);

            #region Internal

            void AddStacks(ItemId itemId, int remainingCount)
            {
                var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(itemId);
                Assert.Greater(maxStack, 0);
                while (0 < remainingCount)
                {
                    Assert.Less(slot, _inventory.GetSlotSize(), "Rail test materials must fit in the inventory");
                    var stackCount = Math.Min(remainingCount, maxStack);
                    _inventory.SetItem(slot, ServerContext.ItemStackFactory.Create(itemId, stackCount));
                    remainingCount -= stackCount;
                    slot++;
                }
            }

            #endregion
        }

        protected void Send(ConnectionDestination from, ConnectionDestination to, Guid connectToolGuid)
        {
            // SendOnly前提のプロトコルなので応答パケットは返らない
            // The protocol is SendOnly, so no response packet comes back
            var request = new RailConnectByDestinationProtocol.RailConnectByDestinationRequest(from, to, connectToolGuid);
            var responses = _environment.PacketResponseCreator.GetPacketResponse(MessagePackSerializer.Serialize(request), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
            Assert.AreEqual(0, responses.Count);
        }

        protected int CountItem(ItemId itemId)
        {
            return _inventory.InventoryItems.Where(stack => stack.Id == itemId).Sum(stack => stack.Count);
        }

        protected void AssertNotification(string messageId)
        {
            // 拒否通知は要求者だけに届き、成功や既接続には出ない
            // Denials reach only the requester; success and existing connections emit none
            var messages = _requesterSink.TakeAll().Where(e => e.Tag == NotificationService.EventTag)
                .Select(e => MessagePackSerializer.Deserialize<NotificationMessagePack>(e.Payload)).ToArray();
            if (messageId == null) Assert.IsEmpty(messages);
            else Assert.AreEqual(messageId, messages.Single().MessageId);
            Assert.IsEmpty(_otherSink.TakeAll().Where(e => e.Tag == NotificationService.EventTag));
        }
    }
}
