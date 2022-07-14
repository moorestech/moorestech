using Game.PlayerInventory.Interface.Event;
using Game.Quest.Interface;
using Game.Quest.QuestEntity;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.CombinedTest.Game
{
    /// <summary>
    /// クエストをクリアすることでクエスト一覧が更新されるテスト
    /// </summary>
    public class QuestCompletedTest
    {
        private const int PlayerId = 1;
        
        [Test]
        public void CraftToQuestCompleteTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var questDatastore = serviceProvider.GetService<IQuestDataStore>();
            var craftingEvent = serviceProvider.GetService<ICraftingEvent>();
            
            //クエストの作成と取得
            var quest = (ItemCraftQuest)questDatastore.GetPlayerQuestProgress(PlayerId)[0];
            
            

            //TODO 
        }
    }
}