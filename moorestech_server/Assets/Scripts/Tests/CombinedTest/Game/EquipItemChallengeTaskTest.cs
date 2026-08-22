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

namespace Tests.CombinedTest.Game
{
    public class EquipItemChallengeTaskTest
    {
        private const int PlayerId = 0;
        private static readonly Guid ChallengeGuid = Guid.Parse("00000000-0000-0000-4567-000000000102");
        private static readonly Guid Test1ItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private static readonly Guid UnrelatedItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");

        // 選択中の装備スロットへ入れた瞬間に達成する
        // Putting the item into the selected equipment slot completes the challenge immediately
        [Test]
        public void EquippingIntoSelectedSlotCompletesChallenge()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();
            var equipment = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).EquipmentInventory;

            equipment.SetSelectedEquipmentIndex(0);
            equipment.SetItem(0, MasterHolder.ItemMaster.GetItemId(Test1ItemGuid), 1);

            Assert.IsTrue(IsCompleted(challengeDatastore));
        }

        // 非選択スロットに入れただけでは未達成、そのスロットを選択した時点で達成する
        // A non-selected slot does not count; selecting that slot completes it
        [Test]
        public void NonSelectedSlotDoesNotCountUntilSelected()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();
            var equipment = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).EquipmentInventory;

            equipment.SetSelectedEquipmentIndex(0);
            equipment.SetItem(1, MasterHolder.ItemMaster.GetItemId(Test1ItemGuid), 1);
            GameUpdater.UpdateOneTick();
            Assert.IsFalse(IsCompleted(challengeDatastore));

            equipment.SetSelectedEquipmentIndex(1);
            Assert.IsTrue(IsCompleted(challengeDatastore));
        }

        // 別アイテムを装備しても達成しない
        // Equipping a different item does not complete the challenge
        [Test]
        public void EquippingUnrelatedItemDoesNotCompleteChallenge()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();
            var equipment = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).EquipmentInventory;

            equipment.SetSelectedEquipmentIndex(0);
            equipment.SetItem(0, MasterHolder.ItemMaster.GetItemId(UnrelatedItemGuid), 1);
            GameUpdater.UpdateOneTick();

            Assert.IsFalse(IsCompleted(challengeDatastore));
        }

        // チャレンジ開始前に装備済みなら初回tickで回収される
        // An item already equipped before the challenge starts is recovered on the first tick
        [Test]
        public void AlreadyEquippedCompletesOnFirstTick()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            var equipment = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).EquipmentInventory;
            equipment.SetSelectedEquipmentIndex(0);
            equipment.SetItem(0, MasterHolder.ItemMaster.GetItemId(Test1ItemGuid), 1);

            challengeDatastore.InitializeCurrentChallenges();
            Assert.IsFalse(IsCompleted(challengeDatastore));
            GameUpdater.UpdateOneTick();

            Assert.IsTrue(IsCompleted(challengeDatastore));
        }

        private static bool IsCompleted(ChallengeDatastore challengeDatastore)
        {
            return challengeDatastore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == ChallengeGuid);
        }
    }
}
