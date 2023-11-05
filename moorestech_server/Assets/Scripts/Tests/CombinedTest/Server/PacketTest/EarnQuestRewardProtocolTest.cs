using System.Linq;
using Core.Const;
using Game.PlayerInventory.Interface;
using Game.Quest.Interface;
using Game.Quest.QuestEntity;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    ///     報酬受け取りのテスト
    /// </summary>
    public class EarnQuestRewardProtocolTest
    {
        private const int PlayerId = 1;
        private const int RewardQuestIndex = 1;

        /// <summary>
        ///     通常のクエスト報酬受け取りテスト
        /// </summary>
        [Test]
        public void NormalEarnTest()
        {
            var (packet, serviceProvider) =
                new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var quest =
                (ItemCraftQuest)serviceProvider.GetService<IQuestDataStore>().GetPlayerQuestProgress(PlayerId)[
                    RewardQuestIndex];
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);


            //クリアになって無いとき報酬を受け取れない時のテスト
            //報酬受け取りのパケットを送信
            packet.GetPacketResponse(MessagePackSerializer
                .Serialize(new EarnQuestRewardMessagePack(PlayerId, quest.QuestConfig.QuestId)).ToList());
            //報酬が受け取れていないことを確認
            Assert.AreEqual(false, quest.IsEarnedReward);
            //アイテムが入っていないことを確認
            Assert.AreEqual(ItemConst.EmptyItemId, playerInventory.MainOpenableInventory.Items[0].Id);


            //通常通り報酬を受け取れるテスト
            //クリアした状態をリフレクションで設定
            typeof(ItemCraftQuest).GetProperty("IsCompleted").SetValue(quest, true);

            //報酬受け取りのパケットを送信
            packet.GetPacketResponse(MessagePackSerializer
                .Serialize(new EarnQuestRewardMessagePack(PlayerId, quest.QuestConfig.QuestId)).ToList());

            //報酬が入っているか確認
            Assert.AreEqual(quest.QuestConfig.RewardItemStacks[0], playerInventory.MainOpenableInventory.Items[0]);
            //報酬が受け取り済みかどうかをテスト
            Assert.AreEqual(true, quest.IsEarnedReward);


            //報酬は複数受け取れない時のテスト
            //報酬受け取りのパケットを送信
            packet.GetPacketResponse(MessagePackSerializer
                .Serialize(new EarnQuestRewardMessagePack(PlayerId, quest.QuestConfig.QuestId)).ToList());
            //アイテムが入っていないことを確認する
            Assert.AreEqual(quest.QuestConfig.RewardItemStacks[0], playerInventory.MainOpenableInventory.Items[0]);
        }


        /// <summary>
        ///     インベントリが満タンでアイテムを受け取れないテスト
        /// </summary>
        [Test]
        public void ItemFullNotEarnTest()
        {
            var (packet, serviceProvider) =
                new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var quest =
                (ItemCraftQuest)serviceProvider.GetService<IQuestDataStore>().GetPlayerQuestProgress(PlayerId)[
                    RewardQuestIndex];
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId)
                .MainOpenableInventory;

            //クリアした状態をリフレクションで設定
            typeof(ItemCraftQuest).GetProperty("IsCompleted").SetValue(quest, true);

            //報酬以外のインベントリを満タンにする
            for (var i = 0; i < mainInventory.GetSlotSize(); i++)
            {
                var differItemId = quest.QuestConfig.RewardItemStacks[0].Id + 1;
                mainInventory.SetItem(i, differItemId, 1);
            }


            //報酬受け取りのパケットを送信
            packet.GetPacketResponse(MessagePackSerializer
                .Serialize(new EarnQuestRewardMessagePack(PlayerId, quest.QuestConfig.QuestId)).ToList());
            //報酬が受け取りになっていないテスト
            Assert.AreEqual(false, quest.IsEarnedReward);
        }
    }
}