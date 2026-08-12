using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface.Extension;
using Game.Blueprint;
using Game.Hotbar;
using Game.PlacementTarget;
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

            // マスタの実在ブロックを割当→保持される（坂はカタログ対象外なので除く）
            // Assigning a real master block is retained (slopes are excluded from the catalog)
            var validId = MasterHolder.BlockMaster.Blocks.Data.First(b => !BeltConveyorPlaceFamilyUtil.IsSlopeBlock(b.BlockGuid)).BlockGuid;
            datastore.SetAssignment(playerId: 1, slot: 3, validId);
            Assert.AreEqual(validId, datastore.GetAssignments(1)[3]);

            // 未知のGuidは無視される
            // Unknown GUIDs are ignored
            datastore.SetAssignment(1, 4, Guid.NewGuid());
            Assert.AreEqual(Guid.Empty, datastore.GetAssignments(1)[4]);

            // セーブ→ロード往復
            // Save and reload round-trips
            var saved = datastore.GetSaveJsonObject();
            var datastore2 = new HotbarAssignmentDatastore(catalog, blueprintDatastore);
            datastore2.LoadHotbar(saved);
            Assert.AreEqual(validId, datastore2.GetAssignments(1)[3]);
        }

        [Test]
        public void ロード時に解決できない割当は削除される()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var catalog = serviceProvider.GetService<PlacementTargetCatalog>();
            var blueprintDatastore = serviceProvider.GetService<IBlueprintDatastore>();
            var datastore = new HotbarAssignmentDatastore(catalog, blueprintDatastore);

            // Assignmentsに未知Guidを含むセーブをLoadHotbar→該当枠はGuid.Empty
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

            // BlueprintDatastore.Registerで登録したGuidをSetAssignment→保持される
            // A guid registered via BlueprintDatastore.Register is retained
            var blueprintGuid = blueprintDatastore.Register(new BlueprintJsonObject("hotbar-bp", new List<BlueprintBlockJsonObject>(), Guid.NewGuid()));
            datastore.SetAssignment(2, 0, blueprintGuid);
            Assert.AreEqual(blueprintGuid, datastore.GetAssignments(2)[0]);

            // BP削除後にGetSaveJsonObject→LoadHotbar→該当枠はGuid.Empty
            // After deleting the blueprint, a save/load round-trip clears that slot
            blueprintDatastore.Delete(blueprintGuid);
            var saved = datastore.GetSaveJsonObject();
            var datastore2 = new HotbarAssignmentDatastore(catalog, blueprintDatastore);
            datastore2.LoadHotbar(saved);

            Assert.AreEqual(Guid.Empty, datastore2.GetAssignments(2)[0]);
        }
    }
}
