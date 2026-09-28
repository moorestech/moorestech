using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Blueprint;
using Game.Hotbar;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using Game.PlayerIdentity;
using NUnit.Framework;
using Tests.Util.PlayerIdentity;
using Server.Boot;
using UniRx;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    /// <summary>
    ///     ホットバー割当のセーブ配線とBP削除時のprune契約の回帰試験
    ///     Regression tests for the hotbar's save wiring and the prune contract on blueprint deletion
    /// </summary>
    public class HotbarSaveLoadTest
    {

        // セーブ→ロードで割当が復元される
        // Save then load restores the assignments
        [Test]
        public void SaveLoadRestoresHotbarAssignmentsTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(serviceProvider.GetRequiredService<IPlayerIdentityRegistry>(), "steam:1").PlayerId;
            var datastore = serviceProvider.GetService<HotbarAssignmentDatastore>();
            var blockGuid = ResolvableBlockGuid();
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockBlock(blockGuid);

            datastore.SetAssignment(playerId, 4, blockGuid);
            var saveJson = serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);

            var loaded = loadServiceProvider.GetService<IHotbarAssignmentLookup>().GetAssignments(playerId);
            Assert.AreEqual(blockGuid, loaded[4]);
            Assert.AreEqual(Guid.Empty, loaded[0]);
        }

        // 参照しただけのプレイヤーはセーブへ現れない
        // A player that was only read never appears in the save
        [Test]
        public void ReadingAssignmentsDoesNotPersistAnEmptyRecordTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(serviceProvider.GetRequiredService<IPlayerIdentityRegistry>(), "steam:1").PlayerId;
            var datastore = serviceProvider.GetService<HotbarAssignmentDatastore>();

            var assignments = datastore.GetAssignments(playerId);

            Assert.AreEqual(HotbarAssignmentDatastore.SlotCount, assignments.Count);
            Assert.IsEmpty(datastore.GetSaveJsonObject(), "読み取りだけではレコードを作らない");
        }

        // BP削除で当該BPを指す枠だけが外れる
        // Deleting a blueprint clears only the slots pointing at it
        [Test]
        public void DeletingBlueprintPrunesOnlyItsAssignmentTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(serviceProvider.GetRequiredService<IPlayerIdentityRegistry>(), "steam:1").PlayerId;
            var blueprintDatastore = serviceProvider.GetService<IBlueprintDatastore>();
            var datastore = serviceProvider.GetService<HotbarAssignmentDatastore>();
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();
            unlockState.UnlockBlueprint();

            var blueprintGuid = Guid.Parse("70000000-0000-4000-8000-000000000001");
            blueprintDatastore.Register(new BlueprintJsonObject("starter-base", new List<BlueprintBlockJsonObject>(), blueprintGuid));

            var blockGuid = ResolvableBlockGuid();
            unlockState.UnlockBlock(blockGuid);
            datastore.SetAssignment(playerId, 0, blueprintGuid);
            datastore.SetAssignment(playerId, 1, blockGuid);

            var changedPlayerIds = new List<int>();
            using (datastore.OnAssignmentChanged.Subscribe(changedPlayerIds.Add))
            {
                blueprintDatastore.Delete(blueprintGuid);
            }

            var assignments = datastore.GetAssignments(playerId);
            Assert.AreEqual(Guid.Empty, assignments[0], "削除されたBPを指す枠は外れる");
            Assert.AreEqual(blockGuid, assignments[1], "無関係な枠は残る");
            CollectionAssert.AreEqual(new[] { playerId }, changedPlayerIds, "変化したプレイヤーだけ通知される");
        }

        // カタログで解決できる実在ブロックGuid
        // A real, catalog-resolvable block GUID
        private static Guid ResolvableBlockGuid()
        {
            return MasterHolder.BlockMaster.Blocks.Data.First().BlockGuid;
        }
    }
}
