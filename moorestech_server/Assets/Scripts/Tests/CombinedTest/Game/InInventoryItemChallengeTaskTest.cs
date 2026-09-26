using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Challenge;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Tests.CombinedTest.Game
{
    public class InInventoryItemChallengeTaskTest
    {
        // テスト2: Test1アイテムを3個所持で達成
        // Challenge "テスト2": completes when holding 3 of Test1Item
        private static readonly Guid ChallengeGuid = Guid.Parse("00000000-0000-0000-4567-000000000002");
        private static readonly Guid Test1ItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private const int FirstRegisteredEmptyPlayerId = 1;
        private const int SecondPlayerId = 1623179277;

        // 先に登録された空のプレイヤーが居ても、2人目の所持で達成する
        // An empty player registered first must not block completion by the second player's items
        [Test]
        public void EmptyFirstPlayerDoesNotBlockSecondPlayersItems()
        {
            var (challengeDatastore, inventoryDataStore) = CreateServer();
            inventoryDataStore.GetInventoryData(FirstRegisteredEmptyPlayerId);
            inventoryDataStore.GetInventoryData(SecondPlayerId).MainOpenableInventory.SetItem(0, MasterHolder.ItemMaster.GetItemId(Test1ItemGuid), 3);

            Assert.AreEqual(FirstRegisteredEmptyPlayerId, inventoryDataStore.GetAllPlayerId().First());
            GameUpdater.UpdateOneTick();

            Assert.IsTrue(IsCompleted(challengeDatastore));
        }

        // ワールド共通チャレンジなので全プレイヤーの合算で判定する
        // A world-wide challenge counts the sum over all players
        [Test]
        public void SumAcrossPlayersCompletesChallenge()
        {
            var (challengeDatastore, inventoryDataStore) = CreateServer();
            var itemId = MasterHolder.ItemMaster.GetItemId(Test1ItemGuid);
            inventoryDataStore.GetInventoryData(FirstRegisteredEmptyPlayerId).MainOpenableInventory.SetItem(0, itemId, 2);
            inventoryDataStore.GetInventoryData(SecondPlayerId).MainOpenableInventory.SetItem(0, itemId, 1);

            GameUpdater.UpdateOneTick();

            Assert.IsTrue(IsCompleted(challengeDatastore));
        }

        // 合計が足りなければ未達成
        // Not completed while the total is short
        [Test]
        public void InsufficientTotalDoesNotCompleteChallenge()
        {
            var (challengeDatastore, inventoryDataStore) = CreateServer();
            var itemId = MasterHolder.ItemMaster.GetItemId(Test1ItemGuid);
            inventoryDataStore.GetInventoryData(FirstRegisteredEmptyPlayerId).MainOpenableInventory.SetItem(0, itemId, 1);
            inventoryDataStore.GetInventoryData(SecondPlayerId).MainOpenableInventory.SetItem(0, itemId, 1);

            GameUpdater.UpdateOneTick();

            Assert.IsFalse(IsCompleted(challengeDatastore));
        }

        // 条件到達後に残りスロットがあっても達成通知は1回だけ
        // The completion notification fires once even when more matching slots follow
        [Test]
        public void CompletionIsNotifiedOnlyOnce()
        {
            var (challengeDatastore, inventoryDataStore) = CreateServer();
            var itemId = MasterHolder.ItemMaster.GetItemId(Test1ItemGuid);
            var mainInventory = inventoryDataStore.GetInventoryData(FirstRegisteredEmptyPlayerId).MainOpenableInventory;
            mainInventory.SetItem(0, itemId, 3);
            mainInventory.SetItem(1, itemId, 3);

            var task = challengeDatastore.CurrentChallengeInfo.CurrentChallenges.First(c => c.ChallengeMasterElement.ChallengeGuid == ChallengeGuid);
            var notifiedCount = 0;
            task.OnChallengeComplete.Subscribe(_ => notifiedCount++);
            GameUpdater.UpdateOneTick();

            Assert.AreEqual(1, notifiedCount);
        }

        private static (ChallengeDatastore, IPlayerInventoryDataStore) CreateServer()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();
            return (challengeDatastore, serviceProvider.GetService<IPlayerInventoryDataStore>());
        }

        private static bool IsCompleted(ChallengeDatastore challengeDatastore)
        {
            return challengeDatastore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == ChallengeGuid);
        }
    }
}
