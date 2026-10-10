using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Blueprint;
using Game.Hotbar;
using Game.PlacementTarget;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.Hotbar
{
    public class BlueprintHotbarAssignmentTest
    {
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
            var blueprintGuid = blueprintDatastore.Register(new BlueprintJsonObject("hotbar-bp", new List<BlueprintBlockJsonObject>(), new List<BlueprintLineJsonObject>(), new List<BlueprintLineJsonObject>(), Guid.NewGuid()));
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
            var blueprintGuid = blueprintDatastore.Register(new BlueprintJsonObject("locked-bp", new List<BlueprintBlockJsonObject>(), new List<BlueprintLineJsonObject>(), new List<BlueprintLineJsonObject>(), Guid.NewGuid()));
            datastore.SetAssignment(1, 0, blueprintGuid);

            Assert.AreEqual(Guid.Empty, datastore.GetAssignments(1)[0]);
        }

        private static (global::Server.Protocol.PacketResponseCreator packet, ServiceProvider serviceProvider) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
