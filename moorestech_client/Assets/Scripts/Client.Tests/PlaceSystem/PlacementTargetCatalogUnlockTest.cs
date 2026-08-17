using System;
using System.Linq;
using Game.PlacementTarget;
using Game.Context;
using Game.UnlockState;
using NUnit.Framework;
using Server.Boot;
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
    }
}
