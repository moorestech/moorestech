using System;
using System.Collections.Generic;
using System.IO;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Gear;
using Tests.Util;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using Game.World.Interface.DataStore;
using static Tests.CombinedTest.Core.Transport.Segment.BeltWorldTransportTest;

namespace Tests.CombinedTest.Core.Transport.Segment
{
    public class BeltLoadedSpeedBoundaryTest : IBeltItemDropObserver
    {
        public void OnDropped(BeltCellItemState item, string reason) => Assert.Fail(reason);

        [TestCase(true, false)]
        [TestCase(false, false)]
        [TestCase(true, true)]
        public void HeterogeneousSpeedOverlapSurvivesLoadOrStopTest(bool captureBeforeTick, bool stopBeforeLoad)
        {
            string directory = Path.Combine(Path.GetTempPath(), "belt-speed-load-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "save.json");
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory) {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, path) };
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(options);
            services.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            var first = Place(ForUnitTestModBlockId.GearBeltConveyorSplitter, 0, 0, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            var second = Place(ForUnitTestModBlockId.GearBeltConveyor, 0, 1, BlockDirection.North).GetComponent<VanillaBeltConveyorComponent>();
            var generator = Place(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, 2, 1, BlockDirection.East).GetComponent<SimpleGearGeneratorComponent>();
            Place(ForUnitTestModBlockId.SmallGear, 1, 1, BlockDirection.East);
            Place(ForUnitTestModBlockId.SmallGear, 1, 0, BlockDirection.East);
            Seed(first, 101);
            Seed(second, 102);
            GameUpdater.UpdateOneTick();
            var transport = services.GetRequiredService<BeltWorldTransport>();
            Assert.AreEqual(128, first.Speed);
            Assert.AreEqual(32, second.Speed);
            Assert.AreEqual(2, transport.Network.CaptureItems().Length);
            Assert.AreEqual(0u, first.BeltConveyorItems[0].RemainingTicks);
            Assert.AreEqual(96u, second.BeltConveyorItems[0].RemainingTicks);
            if (stopBeforeLoad)
            {
                generator.SetGenerateTorque(0f);
                GameUpdater.UpdateOneTick();
                Assert.AreEqual(2, transport.Network.CaptureItems().Length, "Blackout must preserve the valid boundary overlap.");
                ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(2, 0, 1), BlockRemoveReason.ManualRemove);
                GameUpdater.UpdateOneTick();
                Assert.AreEqual(0, first.Speed);
                Assert.AreEqual(0, second.Speed);
            }
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, services.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());

            // 通常のディスクロード後、初回同期がgear tickより先に来る場合も検査する。
            // Test normal disk loading with an initial sync request before the first gear tick as well.
            var (_, loadedServices) = new MoorestechServerDIContainerGenerator().Create(options);
            loadedServices.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            var loaded = loadedServices.GetRequiredService<BeltWorldTransport>();
            int firstChanges = 0, secondChanges = 0;
            ServerContext.WorldBlockDatastore.GetBlock(new BlockInstanceId(first.CellId)).GetComponent<VanillaBeltConveyorComponent>().OnItemsChanged.Subscribe(_ => firstChanges++);
            ServerContext.WorldBlockDatastore.GetBlock(new BlockInstanceId(second.CellId)).GetComponent<VanillaBeltConveyorComponent>().OnItemsChanged.Subscribe(_ => secondChanges++);
            if (!captureBeforeTick) GameUpdater.UpdateOneTick();
            var committed = loaded.CaptureCommittedSnapshot();
            Assert.AreEqual(2, committed.Snapshot.Items.Length);
            Assert.AreEqual(1, firstChanges); Assert.AreEqual(1, secondChanges);
            CollectionAssert.AreEquivalent(new[] { 101L, 102L }, Array.ConvertAll(committed.Snapshot.Items, x => BeltTransportIdentity.ToItemInstanceId(x.Item.Guid).AsPrimitive()));
            var replay = new BeltNetworkReplay(committed.Tick, committed.Snapshot, this);
            loaded.OnTickCompleted.Subscribe(difference => replay.Apply(Tests.Util.BeltTransport.BeltWireRoundTrip.Tick(difference)));
            GameUpdater.RunFrames(10);
            Assert.AreEqual(2, loaded.Network.CaptureItems().Length);
            CollectionAssert.AreEqual(loaded.Network.CaptureItems(), replay.Network.CaptureItems());

            #region Internal
            void Seed(VanillaBeltConveyorComponent belt, long id)
            {
                var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1, new ItemInstanceId(id));
                BeltCellSaveCodec.Load(belt, new BeltCellSaveState { PriorityOrder = 0, Items = new List<BeltCellSavedItem> {
                    new BeltCellSavedItem { ItemStack = new ItemStackSaveJsonObject(stack), InstanceId = id,
                        Progress = 128, EntryDirection = (int)BeltDirection.Back, EntryHeight = 0 } } }, 0.4);
            }
            #endregion
        }

    }
}
