#if NET6_0
using System.Reflection;
using Game.PlayerInventory.Interface.Event;
using Game.Quest.Interface;
using Game.Quest.QuestEntity;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PlayerInventory.Event;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.CombinedTest.Game
{
    /// <summary>
    ///     クエストをクリアすることができるかのテスト
    /// </summary>
    public class CraftingQuestCompletedTest
    {
        private const int PlayerId = 1;
        private int _eventInvokeCount;

        /// <summary>
        ///     アイテムクラフトイベントがアイテムをクラフトした時に発火することを確認する
        /// </summary>
        [Test]
        public void CraftToQuestCompleteTest()
        {
            _eventInvokeCount = 0;

            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var questDatastore = serviceProvider.GetService<IQuestDataStore>();
            var craftingEvent = (CraftingEvent)serviceProvider.GetService<ICraftingEvent>();

            //クエストの作成と取得
            var quest = (ItemCraftQuest)questDatastore.GetPlayerQuestProgress(PlayerId)[0];
            //クラフト対象のアイテムをリフレクションで取得
            var questItemId = (int)quest.GetType().GetField("_questItemId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(quest);
            //クエストのイベント購読
            quest.OnQuestCompleted += OnQuestCompleted;


            //クエストがまだクリアされていないことをチェックする
            Assert.IsFalse(quest.IsCompleted);


            //リフレクションでメソッドを取得、実行
            var method = typeof(CraftingEvent).GetMethod("InvokeEvent", BindingFlags.NonPublic | BindingFlags.Instance);
            //クラフトイベントを発火することで擬似的にクラフトを再現する
            method.Invoke(craftingEvent, new object?[] { questItemId, 1 });


            //クエストがクリアされていることをチェックする
            Assert.IsTrue(quest.IsCompleted);
            //１回目のイベントであることをチェックする
            Assert.AreEqual(1, _eventInvokeCount);


            //２回目のクラフトイベント
            method.Invoke(craftingEvent, new object?[] { questItemId, 1 });


            //イベントが発火されていないことをチェックする
            Assert.AreEqual(1, _eventInvokeCount);
        }

        private void OnQuestCompleted(QuestConfigData obj)
        {
            _eventInvokeCount++;
        }
    }
}
#endif