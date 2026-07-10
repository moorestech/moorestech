using System;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.CleanRoom.Machine;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom;
using Game.Context;
using Game.EnergySystem;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util.MachineRecipe;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomSaveLoadTest
    {
        private static readonly Vector3Int RoomCell = new(1, 1, 1);
        private static readonly Vector3Int MachinePosition = new(2, 1, 1);

        [Test]
        public void RoomImpurityAndClassSurviveSaveLoadTest()
        {
            var (_, datastore1, assembleSaveJsonText1, _) = CreateBlockTestModule();
            var filter = BuildSmallCleanRoomWithFilter();

            // 清浄度がOut以外へ到達した部屋の状態を保存対象にする
            // Save a room only after it has reached a real non-Out clean class
            TickUntilRealCleanClass(filter, datastore1, RoomCell, 800);
            Assert.IsTrue(datastore1.TryGetCleanRoomAt(RoomCell, out var room1));
            Assert.Less(room1.ThresholdIndex, MasterHolder.CleanRoomMaster.OutThresholdIndex);
            var expectedImpurity = room1.ImpurityCount;
            var expectedClassName = MasterHolder.CleanRoomMaster.Thresholds[room1.ThresholdIndex].ClassName;

            // 通常の保存JSONから新しいDIへロードし、復元経路だけを検証する
            // Load the normal save JSON into a fresh DI and verify only the restore path
            var json = assembleSaveJsonText1.AssembleSaveJson();
            var (_, datastore2, _, loader2) = CreateBlockTestModule();
            loader2.Load(json);

            Assert.IsTrue(datastore2.TryGetCleanRoomAt(RoomCell, out var room2));
            Assert.AreEqual(expectedImpurity, room2.ImpurityCount, 0.0001);
            Assert.AreEqual(expectedClassName, MasterHolder.CleanRoomMaster.Thresholds[room2.ThresholdIndex].ClassName);
            Assert.Less(room2.ThresholdIndex, MasterHolder.CleanRoomMaster.OutThresholdIndex);
        }

        [Test]
        public void SaveWithoutCleanRoomRoomsKeyLoadsAsDefaultRoomStateTest()
        {
            var (_, _, assembleSaveJsonText1, _) = CreateBlockTestModule();
            BuildSmallCleanRoomWithFilter();

            // 旧セーブを模擬するため、清浄室状態のキーだけをJSONから削る
            // Remove only the clean-room state key to simulate an old save
            var json = assembleSaveJsonText1.AssembleSaveJson();
            var root = JsonNode.Parse(json) as JsonObject;
            Assert.IsNotNull(root);
            root.Remove("cleanRoomRooms");
            var strippedJson = root.ToJsonString();

            // 古いJSONでもロードは成功し、部屋形状はブロックから再検出される
            // Old JSON must still load, while room geometry is detected from blocks
            var (_, datastore2, _, loader2) = CreateBlockTestModule();
            Assert.DoesNotThrow(() => loader2.Load(strippedJson));
            Assert.IsTrue(datastore2.TryGetCleanRoomAt(RoomCell, out var room2));
            Assert.AreEqual(0, room2.ImpurityCount);
            Assert.AreEqual(MasterHolder.CleanRoomMaster.OutThresholdIndex, room2.ThresholdIndex);
        }

        [Test]
        public void RoomCellsUseNamedCoordinatesInSaveJsonTest()
        {
            var (_, datastore, assembleSaveJsonText, _) = CreateBlockTestModule();
            BuildSmallCleanRoomWithFilter();
            datastore.RebuildAll();
            var cell = JsonNode.Parse(assembleSaveJsonText.AssembleSaveJson())["cleanRoomRooms"][0]["cells"][0];
            CollectionAssert.AreEquivalent(new[] { "x", "y", "z" }, cell.AsObject().Select(pair => pair.Key));
        }

        [Test]
        public void ProcessingMachineStateSurvivesSaveLoadTest()
        {
            var (_, _, assembleSaveJsonText1, _) = CreateBlockTestModule();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine1 = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomMachineId, MachinePosition);
            MachineRecipeSelectionTestUtil.SelectRecipe(machine1, ForUnitTestMachineRecipeId.CleanRoomMachineRecipe);

            // 入力を入れ、清浄室内で実際にProcessingへ入るまで満電で進める
            // Load inputs and power the room until the machine really enters Processing
            machine1.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ForUnitTestItemId.TestChipRawWafer, 5);
            var processor1 = machine1.GetComponent<CleanRoomMachineProcessorComponent>();
            TickUntilProcessing(filter, machine1, processor1, 200);
            Assert.AreEqual(ProcessState.Processing, processor1.CurrentState);

            // 中間進捗を作ってから、残tickとレシピGUIDを保存前の期待値にする
            // Accumulate partial progress, then capture remaining ticks and recipe GUID before saving
            for (var i = 0; i < 5; i++) TickRoom(filter, machine1);
            var expectedRemainingTicks = processor1.GetRemainingTicks();
            var expectedRecipeGuid = processor1.RecipeGuid;
            Assert.Greater(expectedRemainingTicks, 0);

            // 新DIへロードして同じ位置の機械プロセッサを取り直す
            // Load into a new DI and fetch the processor from the same block position
            var json = assembleSaveJsonText1.AssembleSaveJson();
            var (_, _, _, loader2) = CreateBlockTestModule();
            loader2.Load(json);
            var loadedMachine = ServerContext.WorldBlockDatastore.GetBlock(MachinePosition);
            var processor2 = loadedMachine.GetComponent<CleanRoomMachineProcessorComponent>();

            Assert.AreEqual(ProcessState.Processing, processor2.CurrentState);
            Assert.AreEqual(expectedRecipeGuid, processor2.RecipeGuid);
            Assert.AreEqual(expectedRemainingTicks, processor2.GetRemainingTicks());
        }

        #region TestHelper

        private static (IWorldBlockDatastore, CleanRoomDatastore, AssembleSaveJsonText, WorldLoaderFromJson) CreateBlockTestModule()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // DI生成後のServerContextが指すインスタンスと保存ロードサービスを束ねる
            // Bundle the ServerContext instances and save/load services after DI creation
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var cleanRoomDatastore = serviceProvider.GetService<CleanRoomDatastore>();
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var loadJsonFile = serviceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            return (worldBlockDatastore, cleanRoomDatastore, assembleSaveJsonText, loadJsonFile);
        }

        private static IBlock BuildSmallCleanRoomWithFilter()
        {
            // 内寸3x3x3の密閉室に清浄機を置き、フィルターを装填する
            // Build a sealed 3x3x3-interior room with a loaded air filter
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            var filter = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, RoomCell);
            filter.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ForUnitTestItemId.TestCleanRoomFilter, 5);
            return filter;
        }

        private static void TickUntilRealCleanClass(IBlock filter, CleanRoomDatastore datastore, Vector3Int cell, int maxTicks)
        {
            for (var i = 0; i < maxTicks && !HasRealCleanClass(datastore, cell); i++) TickFilter(filter);
        }

        private static bool HasRealCleanClass(CleanRoomDatastore datastore, Vector3Int cell)
        {
            return datastore.TryGetCleanRoomAt(cell, out var room) &&
                   room.ThresholdIndex < MasterHolder.CleanRoomMaster.OutThresholdIndex;
        }

        private static void TickUntilProcessing(IBlock filter, IBlock machine, CleanRoomMachineProcessorComponent processor, int maxTicks)
        {
            for (var i = 0; i < maxTicks && processor.CurrentState != ProcessState.Processing; i++) TickRoom(filter, machine);
        }

        private static void TickFilter(IBlock filter)
        {
            // 清浄機だけに毎tick満電を供給し、室内純度を進める
            // Supply full power to the filter each tick to advance room purity
            filter.GetComponent<IElectricConsumer>().SupplyEnergy(new ElectricPower(100f));
            GameUpdater.UpdateOneTick();
        }

        private static void TickRoom(IBlock filter, IBlock machine)
        {
            // 清浄機と機械を同じtickで満電にし、通常の室内加工経路を通す
            // Fully power the filter and machine in the same tick for normal in-room processing
            filter.GetComponent<IElectricConsumer>().SupplyEnergy(new ElectricPower(100f));
            machine.GetComponent<IElectricConsumer>().SupplyEnergy(new ElectricPower(100f));
            GameUpdater.UpdateOneTick();
        }

        #endregion
    }

    public static class CleanRoomMachineProcessorTestUtil
    {
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // 非公開_processingStateが保持する残tickをテストから読む
        // Read remaining ticks held by the non-public _processingState from tests
        public static uint GetRemainingTicks(this CleanRoomMachineProcessorComponent processor)
        {
            var processingState = typeof(CleanRoomMachineProcessorComponent)
                .GetField("_processingState", InstanceFlags)
                .GetValue(processor);
            return (uint)processingState.GetType().GetProperty("RemainingTicks", InstanceFlags).GetValue(processingState);
        }
    }
}
