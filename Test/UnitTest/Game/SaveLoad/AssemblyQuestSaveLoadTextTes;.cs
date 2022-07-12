using System;
using Game.Quest.Interface;
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
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.QuestTestModDirectory);
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var questDataStore = serviceProvider.GetService<IQuestDataStore>();

            questDataStore.GetPlayerQuestProgress(playerId);
            
            
            
            
            
            //セーブの実行
            var json = assembleSaveJsonText.AssembleSaveJson();
            Console.WriteLine(json);

            //ロードの実行
            var (_, loadServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.QuestTestModDirectory);
            (loadServiceProvider.GetService<ILoadRepository>() as LoadJsonFile).Load(json);
        }
    }
}