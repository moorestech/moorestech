using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface.Extension;
using Game.Blueprint;
using Game.Hotbar;
using Game.PlacementTarget;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    public class HotbarAssignmentDatastoreTest
    {
        [Test]
        public void 割当はカタログ解決できるGuidのみ受け付けセーブロードで往復する()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = serviceProvider.GetService<HotbarAssignmentDatastore>();
            var catalog = serviceProvider.GetService<PlacementTargetCatalog>();
            var blueprintDatastore = serviceProvider.GetService<IBlueprintDatastore>();
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();

            // マスタの実在ブロックを解放して割当→保持される（坂はカタログ対象外なので除く。C1裁定でブロックの割当も解放判定の対象）
            // Unlock a real master block, then assigning it is retained (slopes excluded from the catalog; C1 ruling gates block assignment on unlock too)
            var validId = MasterHolder.BlockMaster.Blocks.Data.First(b => !BeltConveyorPlaceFamilyUtil.IsSlopeBlock(b.BlockGuid)).BlockGuid;
            unlockState.UnlockBlock(validId);
            datastore.SetAssignment(playerId: 1, slot: 3, validId);
            Assert.AreEqual(validId, datastore.GetAssignments(1)[3]);

            // 未知のGuidは無視される
            // Unknown GUIDs are ignored
            datastore.SetAssignment(1, 4, Guid.NewGuid());
            Assert.AreEqual(Guid.Empty, datastore.GetAssignments(1)[4]);

            // セーブ→ロード往復
            // Save and reload round-trips
            var saved = datastore.GetSaveJsonObject();
            var datastore2 = new HotbarAssignmentDatastore(catalog, blueprintDatastore, unlockState);
            datastore2.LoadHotbar(saved);
            Assert.AreEqual(validId, datastore2.GetAssignments(1)[3]);
        }

        [Test]
        public void ロード時に解決できない割当は削除される()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var catalog = serviceProvider.GetService<PlacementTargetCatalog>();
            var blueprintDatastore = serviceProvider.GetService<IBlueprintDatastore>();
            var datastore = new HotbarAssignmentDatastore(catalog, blueprintDatastore, serviceProvider.GetService<IGameUnlockStateDataController>());

            // 未知Guid含むセーブはEmpty化
            // Loading a save containing an unknown GUID clears that slot
            var assignments = Enumerable.Repeat(Guid.Empty.ToString(), HotbarAssignmentDatastore.SlotCount).ToList();
            assignments[2] = Guid.NewGuid().ToString();
            var saveData = new List<PlayerHotbarSaveJsonObject> { new(1, assignments) };

            datastore.LoadHotbar(saveData);

            Assert.AreEqual(Guid.Empty, datastore.GetAssignments(1)[2]);
        }

        [Test]
        public void ブループリントのGuidも割当でき削除後のロードでは消える()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = serviceProvider.GetService<HotbarAssignmentDatastore>();
            var catalog = serviceProvider.GetService<PlacementTargetCatalog>();
            var blueprintDatastore = serviceProvider.GetService<IBlueprintDatastore>();
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();
            unlockState.UnlockBlueprint();

            // 登録Guidを割当→保持確認
            // A guid registered via BlueprintDatastore.Register is retained
            var blueprintGuid = blueprintDatastore.Register(new BlueprintJsonObject("hotbar-bp", new List<BlueprintBlockJsonObject>(), Guid.NewGuid()));
            datastore.SetAssignment(2, 0, blueprintGuid);
            Assert.AreEqual(blueprintGuid, datastore.GetAssignments(2)[0]);

            // BP削除後の再読込でEmpty化
            // After deleting the blueprint, a save/load round-trip clears that slot
            blueprintDatastore.Delete(blueprintGuid);
            var saved = datastore.GetSaveJsonObject();
            var datastore2 = new HotbarAssignmentDatastore(catalog, blueprintDatastore, unlockState);
            datastore2.LoadHotbar(saved);

            Assert.AreEqual(Guid.Empty, datastore2.GetAssignments(2)[0]);
        }

        [Test]
        public void 範囲外slotへの操作は無視され例外にならない()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = serviceProvider.GetService<HotbarAssignmentDatastore>();
            var validId = MasterHolder.BlockMaster.Blocks.Data.First(b => !BeltConveyorPlaceFamilyUtil.IsSlopeBlock(b.BlockGuid)).BlockGuid;

            // 範囲外slotへのSet/Clear/Swapは例外を投げず無視される（不正クライアント対策はtargetIdと同じ扱い）
            // Out-of-range slots are ignored without throwing for Set/Clear/Swap, matching the targetId defense
            Assert.DoesNotThrow(() => datastore.SetAssignment(3, HotbarAssignmentDatastore.SlotCount, validId));
            Assert.DoesNotThrow(() => datastore.SetAssignment(3, -1, validId));
            Assert.DoesNotThrow(() => datastore.ClearAssignment(3, HotbarAssignmentDatastore.SlotCount));
            Assert.DoesNotThrow(() => datastore.SwapAssignments(3, 0, HotbarAssignmentDatastore.SlotCount));

            CollectionAssert.AreEqual(new Guid[HotbarAssignmentDatastore.SlotCount], datastore.GetAssignments(3));
        }

        [Test]
        public void 形状不正なセーブをロードしても例外にならず解決不能枠はGuidEmptyになる()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var catalog = serviceProvider.GetService<PlacementTargetCatalog>();
            var blueprintDatastore = serviceProvider.GetService<IBlueprintDatastore>();
            var datastore = new HotbarAssignmentDatastore(catalog, blueprintDatastore, serviceProvider.GetService<IGameUnlockStateDataController>());

            // 件数不足/パース不能セーブを検証
            // Load saves whose Assignments count isn't 9, and whose entries aren't parseable GUIDs
            var shortAssignments = Enumerable.Repeat(Guid.Empty.ToString(), HotbarAssignmentDatastore.SlotCount - 1).ToList();
            var unparsableAssignments = Enumerable.Repeat("not-a-guid", HotbarAssignmentDatastore.SlotCount).ToList();
            var saveData = new List<PlayerHotbarSaveJsonObject>
            {
                new(1, shortAssignments),
                new(2, unparsableAssignments),
            };

            Assert.DoesNotThrow(() => datastore.LoadHotbar(saveData));

            Assert.AreEqual(Guid.Empty, datastore.GetAssignments(1)[0]);
            Assert.AreEqual(Guid.Empty, datastore.GetAssignments(2)[0]);
        }

        [Test]
        public void 未解放時はBP系の新規割当が無視されロード済み割当は保持される()
        {
            // DIから実物一式を取得（初期=未解放）
            // Resolve the real instances from DI (unlock state starts locked)
            var (_, serviceProvider) = CreateServer();
            var datastore = serviceProvider.GetService<HotbarAssignmentDatastore>();
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();
            var catalog = serviceProvider.GetService<PlacementTargetCatalog>();
            var copyToolId = catalog.CreateEntries(Array.Empty<(Guid, string)>())
                .First(entry => entry.Kind == PlacementTargetKind.BlueprintCopy).Id;

            // 未解放: コピーツールの割当は無視される
            // Locked: assigning the copy tool is ignored
            datastore.SetAssignment(1, 0, copyToolId);
            Assert.AreEqual(Guid.Empty, datastore.GetAssignments(1)[0]);

            // 旧セーブ相当: ロックでも保持
            // Old-save equivalent: saved blueprint-tool slots survive a locked load (existence check only)
            unlockState.UnlockBlueprint();
            datastore.SetAssignment(1, 0, copyToolId);
            var save = datastore.GetSaveJsonObject();
            var (_, lockedProvider) = CreateServer();
            var lockedDatastore = lockedProvider.GetService<HotbarAssignmentDatastore>();
            lockedDatastore.LoadHotbar(save);
            Assert.AreEqual(copyToolId, lockedDatastore.GetAssignments(1)[0]);
        }

        [Test]
        public void 未解放時は登録済みBPGuidの新規割当も無視される()
        {
            var (_, serviceProvider) = CreateServer();
            var datastore = serviceProvider.GetService<HotbarAssignmentDatastore>();
            var blueprintDatastore = serviceProvider.GetService<IBlueprintDatastore>();

            // 現行BPのGuidを未解放のまま割当てる
            // A guid resolvable as a current blueprint (not in the master), assigned while still locked
            var blueprintGuid = blueprintDatastore.Register(new BlueprintJsonObject("locked-bp", new List<BlueprintBlockJsonObject>(), Guid.NewGuid()));
            datastore.SetAssignment(1, 0, blueprintGuid);

            Assert.AreEqual(Guid.Empty, datastore.GetAssignments(1)[0]);
        }

        private static (global::Server.Protocol.PacketResponseCreator packet, ServiceProvider serviceProvider) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
