using System;
using System.Linq;
using Game.PlacementTarget;
using Game.Context;
using Game.UnlockState;
using Core.Master;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.CombinedTest.Server.PacketTest;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem
{
    public class PlacementTargetCatalogUnlockTest
    {
        // BPを1件も持たない状態
        // The state with no blueprints at all
        private static readonly (Guid id, string name)[] NoBlueprints = Array.Empty<(Guid, string)>();

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void showAllPlaceableはBlockとTrainCarだけを解放しConnectToolには影響しない()
        {
            var catalog = new PlacementTargetCatalog();
            var unlockState = ServerContext.GetService<IGameUnlockStateDataController>();
            var catalogEntries = catalog.CreateEntries(NoBlueprints);
            var normalIds = catalog.UnlockedEntries(unlockState, false, NoBlueprints).Select(entry => entry.Id).ToHashSet();
            var showAllIds = catalog.UnlockedEntries(unlockState, true, NoBlueprints).Select(entry => entry.Id).ToHashSet();
            var catalogBlockIds = catalogEntries.Where(entry => entry.Kind == PlacementTargetKind.Block).Select(entry => entry.Id).ToHashSet();
            var catalogTrainCarIds = catalogEntries.Where(entry => entry.Kind == PlacementTargetKind.TrainCar).Select(entry => entry.Id).ToHashSet();
            var lockedBlocks = unlockState.BlockUnlockStateInfos.Where(pair => !pair.Value.IsUnlocked && catalogBlockIds.Contains(pair.Key)).ToList();
            var lockedTrainCars = unlockState.TrainCarUnlockStateInfos.Where(pair => !pair.Value.IsUnlocked && catalogTrainCarIds.Contains(pair.Key)).ToList();

            // 未解放Block・車両だけを追加
            // Add only locked blocks and train cars
            Assert.IsNotEmpty(lockedBlocks);
            Assert.IsNotEmpty(lockedTrainCars);
            foreach (var block in lockedBlocks)
            {
                Assert.IsFalse(normalIds.Contains(block.Key));
                Assert.IsTrue(showAllIds.Contains(block.Key));
            }
            foreach (var trainCar in lockedTrainCars)
            {
                Assert.IsFalse(normalIds.Contains(trainCar.Key));
                Assert.IsTrue(showAllIds.Contains(trainCar.Key));
            }

            // ConnectToolは両モード同一
            // Connect-tool sets match in both modes
            foreach (var connectTool in unlockState.ConnectToolUnlockStateInfos)
                Assert.AreEqual(normalIds.Contains(connectTool.Key), showAllIds.Contains(connectTool.Key));
        }

        [Test]
        public void ブループリント未解放ならBP系エントリは列挙されず解放後に現れる()
        {
            var catalog = new PlacementTargetCatalog();
            var unlockState = ServerContext.GetService<IGameUnlockStateDataController>();
            var blueprintGuid = Guid.Parse("70000000-0000-4000-8000-000000000002");
            var blueprints = new[] { (blueprintGuid, "locked-base") };

            // 未解放: コピーツールも保存済みBPも出ない。showAllPlaceableでも出ない（接続ツール同様）
            // Locked: neither the copy tool nor saved blueprints appear, even with showAllPlaceable
            var lockedIds = catalog.UnlockedEntries(unlockState, false, blueprints).Select(entry => entry.Id).ToHashSet();
            var lockedShowAllIds = catalog.UnlockedEntries(unlockState, true, blueprints).Select(entry => entry.Id).ToHashSet();
            var blueprintCopyIds = catalog.CreateEntries(blueprints).Where(entry => entry.Kind == PlacementTargetKind.BlueprintCopy).Select(entry => entry.Id).ToList();
            Assert.IsNotEmpty(blueprintCopyIds);
            foreach (var copyId in blueprintCopyIds)
            {
                Assert.IsFalse(lockedIds.Contains(copyId));
                Assert.IsFalse(lockedShowAllIds.Contains(copyId));
            }
            Assert.IsFalse(lockedIds.Contains(blueprintGuid));

            // 解放後: 両方現れる
            // Unlocked: both appear
            unlockState.UnlockBlueprint();
            var unlockedIds = catalog.UnlockedEntries(unlockState, false, blueprints).Select(entry => entry.Id).ToHashSet();
            foreach (var copyId in blueprintCopyIds) Assert.IsTrue(unlockedIds.Contains(copyId));
            Assert.IsTrue(unlockedIds.Contains(blueprintGuid));
        }

        [Test]
        public void 坂ベルトはカタログに載り直線の解放状態に従う()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var catalog = new PlacementTargetCatalog();
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();

            // 直線の初期解放状態に依存しないよう、明示的にロックしてから始める
            // Start from an explicitly locked state so the initial unlock flag cannot affect the result
            PlaceBlockProtocolTestSupport.LockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);

            var straightGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor).BlockGuid;
            var upGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearBeltConveyorUp).BlockGuid;
            var downGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearBeltConveyorDown).BlockGuid;

            // 坂もマスタ由来エントリとして列挙される
            // Slopes are enumerated as master-derived entries
            var blockIds = catalog.CreateEntries(NoBlueprints)
                .Where(entry => entry.Kind == PlacementTargetKind.Block)
                .Select(entry => entry.Id)
                .ToHashSet();
            Assert.IsTrue(blockIds.Contains(upGuid));
            Assert.IsTrue(blockIds.Contains(downGuid));

            // 直線が未解放なら坂も出ない
            // Slopes stay hidden while the straight block is locked
            var lockedIds = catalog.UnlockedEntries(unlockState, false, NoBlueprints).Select(entry => entry.Id).ToHashSet();
            Assert.IsFalse(lockedIds.Contains(straightGuid));
            Assert.IsFalse(lockedIds.Contains(upGuid));
            Assert.IsFalse(lockedIds.Contains(downGuid));

            // 直線を解放すると坂も同時に現れる
            // Unlocking the straight block reveals the slopes together
            unlockState.UnlockBlock(straightGuid);
            var unlockedIds = catalog.UnlockedEntries(unlockState, false, NoBlueprints).Select(entry => entry.Id).ToHashSet();
            Assert.IsTrue(unlockedIds.Contains(straightGuid));
            Assert.IsTrue(unlockedIds.Contains(upGuid));
            Assert.IsTrue(unlockedIds.Contains(downGuid));
        }
    }
}
