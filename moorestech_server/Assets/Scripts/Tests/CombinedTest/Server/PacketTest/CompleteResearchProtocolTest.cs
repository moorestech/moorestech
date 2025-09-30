using System;
using System.Linq;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Research;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using static Tests.CombinedTest.Game.ResearchDataStoreTest;


namespace Tests.CombinedTest.Server.PacketTest
{
    public class CompleteResearchProtocolTest
    {
        private const int ResearchNodeCount = 4;

        [Test]
        public void CompleteResearchTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            // 研究1の完了テスト
            // Study 1 Completion Test
            AddRequiredItemsForResearch(serviceProvider, Research1Guid);
            var responseData = SendCompleteResearchRequest(packet, Research1Guid);

            Assert.IsTrue(responseData.Success);
            Assert.AreEqual(Research1Guid.ToString(), responseData.CompletedResearchGuidStr);
            AssertResearchNodes(responseData,
                (Research1Guid, ResearchNodeState.Completed),
                (Research2Guid, ResearchNodeState.UnresearchableNotEnoughItem),
                (Research3Guid, ResearchNodeState.UnresearchableAllReasons),
                (Research4Guid, ResearchNodeState.UnresearchableNotEnoughItem));

            // アイテムが消費されたことを確認
            // Confirm that the item has been consumed
            CheckInventoryEmpty(serviceProvider);
            
            // 同じ研究をもう一度完了しようとすると失敗する
            // Fails when trying to complete the same study again
            responseData = SendCompleteResearchRequest(packet, Research1Guid);

            Assert.IsFalse(responseData.Success);
            AssertResearchNodes(responseData,
                (Research1Guid, ResearchNodeState.Completed));
            
            // Research 2の完了テスト（テスト1の完了条件あり）
            // Research 2 Completion Test (with Test 1 completion conditions)
            AddRequiredItemsForResearch(serviceProvider, Research2Guid);
            responseData = SendCompleteResearchRequest(packet, Research2Guid);

            Assert.IsTrue(responseData.Success);
            Assert.AreEqual(Research2Guid.ToString(), responseData.CompletedResearchGuidStr);
            AssertResearchNodes(responseData,
                (Research1Guid, ResearchNodeState.Completed),
                (Research2Guid, ResearchNodeState.Completed));
            
            // Research 4の完了テスト（アイテム不足）
            // Research 4 Completion Test (Insufficient Items)
            AddItem1(serviceProvider, 1);

            responseData = SendCompleteResearchRequest(packet, Research4Guid);

            Assert.IsFalse(responseData.Success);
            AssertResearchNodes(responseData,
                (Research4Guid, ResearchNodeState.UnresearchableNotEnoughItem));
            
            // Research 4の完了テスト（アイテム十分）
            // Research 4 Completion Test (Sufficient Items)
            AddItem1(serviceProvider, 1);

            responseData = SendCompleteResearchRequest(packet, Research4Guid);

            Assert.IsTrue(responseData.Success);
            Assert.AreEqual(Research4Guid.ToString(), responseData.CompletedResearchGuidStr);
            AssertResearchNodes(responseData,
                (Research1Guid, ResearchNodeState.Completed),
                (Research2Guid, ResearchNodeState.Completed),
                (Research4Guid, ResearchNodeState.Completed));

            #region Internal

            void AssertResearchNodes(CompleteResearchProtocol.ResponseCompleteResearchMessagePack response, params (Guid researchGuid, ResearchNodeState expectedState)[] expectations)
            {
                Assert.IsNotNull(response.NodeState.ResearchNodeStates);
                Assert.AreEqual(ResearchNodeCount, response.NodeState.ResearchNodeStates.Count);

                foreach (var (researchGuid, expectedState) in expectations)
                {
                    var nodeState = response.NodeState.ResearchNodeStates
                        .First(node => node.ResearchGuid == researchGuid)
                        .ResearchNodeState;

                    Assert.AreEqual(expectedState, nodeState);
                }
            }

            #endregion
        }

        private void AddRequiredItemsForResearch(ServiceProvider serviceProvider, Guid researchGuid)
        {
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var researchMaster = MasterHolder.ResearchMaster.GetResearch(researchGuid);

            foreach (var consumeItem in researchMaster.ConsumeItems)
            {
                var item = ServerContext.ItemStackFactory.Create(consumeItem.ItemGuid, consumeItem.ItemCount);
                playerInventoryData.MainOpenableInventory.InsertItem(item);
            }
        }

        private void AddItem1(ServiceProvider serviceProvider, int itemCount)
        {
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var itemId1 = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000001"));
            var item = ServerContext.ItemStackFactory.Create(itemId1, itemCount);
            playerInventoryData.MainOpenableInventory.InsertItem(item);
        }
        
        private void CheckInventoryEmpty(ServiceProvider serviceProvider)
        {
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            for (int i = 0; i < playerInventoryData.MainOpenableInventory.GetSlotSize(); i++)
            {
                var item = playerInventoryData.MainOpenableInventory.GetItem(i);
                Assert.IsTrue(item.Id == ItemMaster.EmptyItemId);
            }
        }

        private CompleteResearchProtocol.ResponseCompleteResearchMessagePack SendCompleteResearchRequest(PacketResponseCreator packet, Guid researchGuid)
        {
            var requestData = MessagePackSerializer.Serialize(new CompleteResearchProtocol.RequestCompleteResearchMessagePack(PlayerId, researchGuid)).ToList();
            var response = packet.GetPacketResponse(requestData);
            
            return MessagePackSerializer.Deserialize<CompleteResearchProtocol.ResponseCompleteResearchMessagePack>(response[0].ToArray());
        }
    }
}
