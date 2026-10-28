using System;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.CleanRoom.Machine;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomMachineTestHelpers;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomProcessMachineTestHelpers;

namespace Tests.CombinedTest.Core.CleanRoom.Machine.Save
{
    public class CleanRoomJobSaveRoundTripTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void SaveReloadPreservesJobTimeAndConsumedGradeOrWarnsForLegacy(bool legacy)
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();
            Assert.IsTrue(WaitForClassA(datastore, filter, machine, 4000));
            var inventory = machine.GetComponent<IOpenableBlockInventoryComponent>();
            inventory.SetItem(FirstModuleSlot, MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000005")), 1);
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomLayerExposureRecipe, ForUnitTestItemId.TestGradedWaferG2, 1);
            TopUpIpa(machine, 20);
            TickRoom(filter, machine);
            var processor = machine.GetComponent<CleanRoomMachineProcessorComponent>();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            var before = (CleanRoomMachineProcessorSaveJsonObject)processor.GetSaveState();
            inventory.SetItem(FirstModuleSlot, ServerContext.ItemStackFactory.CreatEmpty());
            var states = SaveLoadJsonTestHelper.ThroughJson(machine.GetSaveState());
            if (legacy)
            {
                var saved = (JObject)states[processor.SaveKey];
                saved.Property("totalTicks").Remove();
                saved.Property("consumedInputItemGuids").Remove();
                saved.Property("pendingRealization").Remove();
                LogAssert.Expect(LogType.Warning, new Regex("確定結果の節が無い"));
                LogAssert.Expect(LogType.Warning, new Regex("確定時間が無い"));
            }

            // JSON往復で加工残量・出力・消費したG2を復元する
            // Restore remaining work, outputs and consumed G2 through a JSON round trip
            var restored = ServerContext.BlockFactory.Load(machine.BlockGuid, machine.BlockInstanceId, states,
                new BlockPositionInfo(new Vector3Int(30, 0, 30), BlockDirection.North, Vector3Int.one));
            var restoredProcessor = restored.GetComponent<CleanRoomMachineProcessorComponent>();
            var after = (CleanRoomMachineProcessorSaveJsonObject)restoredProcessor.GetSaveState();
            Assert.AreEqual(before.RemainingSeconds, after.RemainingSeconds);
            Assert.IsTrue(JToken.DeepEquals(JArray.FromObject(before.PendingOutputs), JArray.FromObject(after.PendingOutputs)));
            if (!legacy) Assert.AreEqual(before.TotalTicks, after.TotalTicks);
            else LogAssert.Expect(LogType.Warning, new Regex("消費した変種が不明で基準品で返却"));
            var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, restoredProcessor.ClearSelectedRecipe(overflow));
            var returned = restored.GetComponent<IOpenableBlockInventoryComponent>().InventoryItems.Concat(overflow.InventoryItems);
            Assert.AreEqual(1, returned.Where(item => item.Id == ForUnitTestItemId.TestGradedWaferG2).Sum(item => item.Count));
            Assert.AreEqual(0, returned.Where(item => item.Id == ForUnitTestItemId.TestGradedWaferG1).Sum(item => item.Count));
        }
    }
}
