#if NET6_0
using System;
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
        private readonly int playerId = 1;

        [Test]
        public void QuestLoadSaveTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var questDataStore = serviceProvider.GetService<IQuestDataStore>();

            var quests = questDataStore.GetPlayerQuestProgress(playerId);

            
            //index 1
            typeof(ItemCraftQuest).GetProperty("IsCompleted").SetValue(quests[1], true);

            //index 2
            typeof(ItemCraftQuest).GetProperty("IsCompleted").SetValue(quests[2], true);
            typeof(ItemCraftQuest).GetProperty("IsEarnedReward").SetValue(quests[2], true);


            
            var json = assembleSaveJsonText.AssembleSaveJson();
            Console.WriteLine(json);


            
            var (_, loadServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(json);

            
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
#endif