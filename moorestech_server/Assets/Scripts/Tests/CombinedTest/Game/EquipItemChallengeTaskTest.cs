using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Challenge;
using Game.Context;
using Game.PlayerInventory.Interface;
using Server.Protocol.PacketResponse.Util.InventoryService;
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

        // 選択中の装備スロットへ入れると次のティックで達成する
        // Putting the item into the selected equipment slot completes the challenge on the next tick
        [Test]
        public void EquippingIntoSelectedSlotCompletesChallenge()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();
            var equipment = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).EquipmentInventory;

            equipment.SetSelectedEquipmentIndex(0);
            equipment.SetItem(0, MasterHolder.ItemMaster.GetItemId(Test1ItemGuid), 1);
            GameUpdater.UpdateOneTick();

            Assert.IsTrue(IsCompleted(challengeDatastore));
        }

        // 非選択に入れただけでは未達成
        // A non-selected slot does not count; selecting that slot completes it
        [Test]
        public void NonSelectedSlotDoesNotCountUntilSelected()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();
            var equipment = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).EquipmentInventory;

            // マスタの初期装備が既に達成条件を満たすため、非装備状態から検証を始める
            // The master's initial equipment already satisfies the goal, so start from an unequipped state
            ClearEquipment(equipment);

            equipment.SetSelectedEquipmentIndex(0);
            equipment.SetItem(1, MasterHolder.ItemMaster.GetItemId(Test1ItemGuid), 1);
            GameUpdater.UpdateOneTick();
            Assert.IsFalse(IsCompleted(challengeDatastore));

            equipment.SetSelectedEquipmentIndex(1);
            GameUpdater.UpdateOneTick();
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

        // 開始前装備済みは初回tickで回収
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

        // メインインベントリからのスロット移動で装備しても、完了はティック境界まで持ち越される
        // Equipping by moving a slot from the main inventory still defers completion to the tick boundary
        [Test]
        public void MovingItemIntoSelectedSlotCompletesOnlyOnTickBoundary()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();
            var inventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var mainInventory = inventoryData.MainOpenableInventory;
            var equipment = inventoryData.EquipmentInventory;

            // マスタの初期装備が既に達成条件を満たすため、非装備状態から検証を始める
            // The master's initial equipment already satisfies the goal, so start from an unequipped state
            ClearEquipment(equipment);

            equipment.SetSelectedEquipmentIndex(0);
            mainInventory.SetItem(0, MasterHolder.ItemMaster.GetItemId(Test1ItemGuid), 1);
            GameUpdater.UpdateOneTick();
            Assert.IsFalse(IsCompleted(challengeDatastore));

            // 移動イベントの最中に完了カスケードが割り込まないことを確かめる
            // Verify the completion cascade never cuts into the in-flight move event
            InventoryItemMoveService.Move(mainInventory, 0, equipment, 0, 1);
            Assert.IsFalse(IsCompleted(challengeDatastore));

            GameUpdater.UpdateOneTick();
            Assert.IsTrue(IsCompleted(challengeDatastore));
        }

        private static void ClearEquipment(IEquipmentInventory equipment)
        {
            for (var slot = 0; slot < equipment.GetSlotSize(); slot++)
                equipment.SetItem(slot, ServerContext.ItemStackFactory.CreatEmpty());
        }

        private static bool IsCompleted(ChallengeDatastore challengeDatastore)
        {
            return challengeDatastore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == ChallengeGuid);
        }
    }
}
