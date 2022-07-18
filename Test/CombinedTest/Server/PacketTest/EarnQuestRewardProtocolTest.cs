using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Const;
using Game.PlayerInventory.Interface;
using Game.Quest.Interface;
using Game.Quest.QuestEntity;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// 報酬受け取りのテスト
    /// </summary>
    public class EarnQuestRewardProtocolTest
    {
        private const int PlayerId = 1;
        
        /// <summary>
        /// 通常のクエスト報酬受け取りテスト
        /// </summary>
        [Test]
        public void NormalEarnTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var quest = (ItemCraftQuest)serviceProvider.GetService<IQuestDataStore>().GetPlayerQuestProgress(PlayerId)[0];
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            
            
            
            //クリアになって無いとき報酬を受け取れない時のテスト
            //報酬受け取りのパケットを送信
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new EarnQuestRewardMessagePack(PlayerId, quest.Quest.QuestId)).ToList());
            //アイテムが入っていないことを確認
            Assert.AreEqual(quest.Quest.RewardItemStacks[0].Id,ItemConst.EmptyItemId);
            //報酬が見受け取り化のテスト
            Assert.AreEqual(false, quest.IsRewarded);
            
            
            
            //通常通り報酬を受け取れるテスト
            //クリアした状態をリフレクションで設定
            typeof(ItemCraftQuest).GetProperty("IsCompleted").
                SetValue(quest, true);
            
            //報酬受け取りのパケットを送信
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new EarnQuestRewardMessagePack(PlayerId, quest.Quest.QuestId)).ToList());

            //報酬が入っているか確認
            Assert.AreEqual(quest.Quest.RewardItemStacks[0],playerInventory.MainOpenableInventory.Items[0]);
            //報酬が受け取り済みかどうかをテスト
            Assert.AreEqual(true, quest.IsRewarded);
            
            
            
            //報酬は複数受け取れない時のテスト
            //報酬受け取りのパケットを送信
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new EarnQuestRewardMessagePack(PlayerId, quest.Quest.QuestId)).ToList());
            //アイテムが入っていないことを確認する
            Assert.AreEqual(quest.Quest.RewardItemStacks[0],playerInventory.MainOpenableInventory.Items[0]);
        }
    }
}