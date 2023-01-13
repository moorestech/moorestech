using System;
using System.Reflection;
using Game.Quest;
using Game.Quest.Interface;
using Game.Quest.QuestEntity;
using Game.Save.Interface;
using Game.Save.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.UnitTest.Game.SaveLoad
{
    public class AssemblyQuestSaveLoadTextTest
    {
        private int playerId = 1;
        
        [Test]
        public void QuestLoadSaveTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var questDataStore = serviceProvider.GetService<IQuestDataStore>();

            var quests = questDataStore.GetPlayerQuestProgress(playerId);
            
            //クエストのステータスを強制的に更新する
            //index 1のクエストを完了しているようにする
            typeof(ItemCraftQuest).GetProperty("IsCompleted").
                SetValue(quests[1], true);
            
            //index 2のクエストを完了、アイテムを渡しているようにする
            typeof(ItemCraftQuest).GetProperty("IsCompleted").
                SetValue(quests[2], true);
            typeof(ItemCraftQuest).GetProperty("IsEarnedReward").
                SetValue(quests[2], true);
            

            //セーブの実行
            var json = assembleSaveJsonText.AssembleSaveJson();
            Console.WriteLine(json);
            
            

            //ロードの実行
            var (_, loadServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(json);
            
            //ロードしたクエストのチェック
            var loadQuests = loadServiceProvider.GetService<IQuestDataStore>().GetPlayerQuestProgress(playerId);
            
            Assert.AreEqual(false, loadQuests[0].IsCompleted);
            Assert.AreEqual(false, loadQuests[0].IsEarnedReward);
            
            Assert.AreEqual(true, loadQuests[1].IsCompleted);
            Assert.AreEqual(false, loadQuests[1].IsEarnedReward);
            
            Assert.AreEqual(true, loadQuests[2].IsCompleted);
            Assert.AreEqual(true, loadQuests[2].IsEarnedReward);
        }
    }
}