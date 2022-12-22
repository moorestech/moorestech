using System.Reflection;
using Game.PlayerInventory.Interface.Event;
using Game.Quest.Interface;
using Game.Quest.Interface.Extension;
using Game.Quest.QuestEntity;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PlayerInventory.Event;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.UnitTest.Game
{
    public class CraftingQuestTest
    {
        private const int PlayerId = 1;
        /// <summary>
        /// 前提クエストがあるとき、前のクエストが終了してから報酬受け取りが可能になるかのテスト
        /// </summary>
        [Test]
        public void OnePreRequestQuestTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var questDataStore = serviceProvider.GetService<IQuestDataStore>();
            var craftEvent = serviceProvider.GetService<ICraftingEvent>();
            
            var test1Quest = (ItemCraftQuest)questDataStore.GetQuestData(PlayerId, "QuestAuthor:forQuestTest:Test1");
            var test2Quest = (ItemCraftQuest)questDataStore.GetQuestData(PlayerId, "QuestAuthor:forQuestTest:Test2");

            
            //全てのクエストがまだ合格していないことをテスト
            Assert.False(test1Quest.IsCompleted);
            Assert.False(test2Quest.IsCompleted);
            
            
            //前提クエストが１つしかないクエストが正しくクリア、クリアにならないことをテストする
            
            //クエスト2のアイテムクラフトイベントをInvoke
            InvokeCraftEventWithReflection(craftEvent,GetQuestIdWithReflection(test2Quest));
           
            //この段階ではまだクエスト2が合格しているが報酬受け取りができない事をテスト
            Assert.False(test2Quest.IsRewardEarnable());
            Assert.True(test2Quest.IsCompleted);
            
            //前提クエストのクエスト1のクリア
            InvokeCraftEventWithReflection(craftEvent,GetQuestIdWithReflection(test1Quest));
            //クエスト1がクリアしたことをテスト
            Assert.True(test1Quest.IsCompleted);
            Assert.True(test1Quest.IsRewardEarnable());
            //クエスト2の報酬受け取りができることをテスト
            Assert.True(test2Quest.IsRewardEarnable());
        }

        
        /// <summary>
        /// And条件の前提クエストが正しく報酬受け取り可能になるかのテスト
        /// </summary>
        [Test]
        public void AndPreRequestQuest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var questDataStore = serviceProvider.GetService<IQuestDataStore>();
            var craftEvent = serviceProvider.GetService<ICraftingEvent>();
            
            var test1Quest = (ItemCraftQuest)questDataStore.GetQuestData(PlayerId, "QuestAuthor:forQuestTest:Test1");
            var test2Quest = (ItemCraftQuest)questDataStore.GetQuestData(PlayerId, "QuestAuthor:forQuestTest:Test2");
            var testAndPreRequestQuest = (ItemCraftQuest)questDataStore.GetQuestData(PlayerId, "QuestAuthor:forQuestTest:Test3");
            
            //全てのクエストがまだ合格していないことをテスト
            Assert.False(test1Quest.IsCompleted);
            Assert.False(test2Quest.IsCompleted);
            Assert.False(testAndPreRequestQuest.IsCompleted);
            
            
            //And条件のクエストをクリア
            InvokeCraftEventWithReflection(craftEvent,GetQuestIdWithReflection(testAndPreRequestQuest));
            
            //クリアはしているが報酬は受け取れないテスト
            Assert.True(testAndPreRequestQuest.IsCompleted);
            Assert.False(testAndPreRequestQuest.IsRewardEarnable());
            
            //クエスト1をクリア
            InvokeCraftEventWithReflection(craftEvent,GetQuestIdWithReflection(test1Quest));
            
            //AND条件のクエストはまだ報酬受け取りは出来ないテスト
            Assert.False(testAndPreRequestQuest.IsRewardEarnable());
            
            //クエスト2をクリア
            InvokeCraftEventWithReflection(craftEvent,GetQuestIdWithReflection(test2Quest));
            
            //AND条件クエストが報酬受け取り可能になるテスト
            Assert.True(testAndPreRequestQuest.IsRewardEarnable());
            
        }
        
        /// <summary>
        /// Or条件の前提クエストが正しく報酬受け取り可能になるかのテスト
        /// </summary>
        [Test]
        public void OrPreRequestQuest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var questDataStore = serviceProvider.GetService<IQuestDataStore>();
            var craftEvent = serviceProvider.GetService<ICraftingEvent>();
            
            var test1Quest = (ItemCraftQuest)questDataStore.GetQuestData(PlayerId, "QuestAuthor:forQuestTest:Test1");
            var testOrPreRequestQuest = (ItemCraftQuest)questDataStore.GetQuestData(PlayerId, "QuestAuthor:forQuestTest:Test4");
            
            //全てのクエストがまだ合格していないことをテスト
            Assert.False(test1Quest.IsCompleted);
            Assert.False(testOrPreRequestQuest.IsCompleted);
            
            
            //Or条件のクエストをクリア
            InvokeCraftEventWithReflection(craftEvent,GetQuestIdWithReflection(testOrPreRequestQuest));
            
            //クリアはしているが報酬は受け取れないテスト
            Assert.True(testOrPreRequestQuest.IsCompleted);
            Assert.False(testOrPreRequestQuest.IsRewardEarnable());
            
            //クエスト1をクリア
            InvokeCraftEventWithReflection(craftEvent,GetQuestIdWithReflection(test1Quest));
            
            //Or条件のクエストなので報酬受け取りは出来るテスト
            Assert.True(testOrPreRequestQuest.IsRewardEarnable());
        }
        /// <summary>
        /// アイテムクラフトのイベントを無理やり発火する
        /// </summary>
        private void InvokeCraftEventWithReflection(ICraftingEvent craftingEvent,int itemId)
        {
            //リフレクションでメソッドを取得、実行
            var method = typeof(CraftingEvent).GetMethod("InvokeEvent", BindingFlags.NonPublic | BindingFlags.Instance);
            //クラフトイベントを発火することで擬似的にクラフトを再現する
            method.Invoke(craftingEvent,new object?[]{itemId,1});
        }
        private int GetQuestIdWithReflection(ItemCraftQuest itemCraftQuest)
        {
            return (int)itemCraftQuest.GetType().GetField("_questItemId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(itemCraftQuest); 
        }
    }
}