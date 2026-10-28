using System;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Inventory;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.CleanRoom.Machine;
using Game.Block.Blocks.CleanRoom.Machine.YieldPreview;
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
            // 旧形式を再保存しても残り30tickを保つ
            // Re-saving a legacy job with 20 computed ticks and 30 remaining ticks must stay loadable
            Assert.AreEqual(20u, GameUpdater.SecondsToTicks(MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestMachineRecipeId.CleanRoomLayerExposureRecipe).Time));
            Assert.AreEqual(30u, GameUpdater.SecondsToTicks(before.RemainingSeconds));
            Assert.AreEqual(before.TotalTicks, after.TotalTicks);
            var savedAgain = SaveLoadJsonTestHelper.ThroughJson(restored.GetSaveState());
            restored = ServerContext.BlockFactory.Load(restored.BlockGuid, restored.BlockInstanceId, savedAgain,
                new BlockPositionInfo(new Vector3Int(40, 0, 40), BlockDirection.North, Vector3Int.one));
            restoredProcessor = restored.GetComponent<CleanRoomMachineProcessorComponent>();
            var afterAgain = (CleanRoomMachineProcessorSaveJsonObject)restoredProcessor.GetSaveState();
            Assert.AreEqual(after.TotalTicks, afterAgain.TotalTicks);
            Assert.AreEqual(after.RemainingSeconds, afterAgain.RemainingSeconds);
            if (legacy) LogAssert.Expect(LogType.Warning, new Regex("消費した変種が不明で基準品で返却"));
            var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, restoredProcessor.ClearSelectedRecipe(overflow));
            var returned = restored.GetComponent<IOpenableBlockInventoryComponent>().InventoryItems.Concat(overflow.InventoryItems);
            Assert.AreEqual(1, returned.Where(item => item.Id == ForUnitTestItemId.TestGradedWaferG2).Sum(item => item.Count));
            Assert.AreEqual(0, returned.Where(item => item.Id == ForUnitTestItemId.TestGradedWaferG1).Sum(item => item.Count));
        }
        [TestCase("waferGrade")]
        [TestCase("layerExposure")]
        [TestCase("dicing")]
        public void DrawRecordPreservesWireShapeAndRatesThroughJson(string kind)
        {
            var wire = JObject.Parse(kind switch
            {
                "waferGrade" => "{ 'kind': 'waferGrade', 'maxGrade': 3, 'promotionChance': 0.125 }",
                "layerExposure" => "{ 'kind': 'layerExposure', 'layerFailRate': 0.25 }",
                "dicing" => "{ 'kind': 'dicing', 'dieLossRate': 0.375 }",
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            });

            // 既存wireを二往復して率を保つ
            // Restore the existing wire format and compare rates after two captures by the concrete role
            var restored = CleanRoomYieldPreviewResult.RestoreDrawRecord(wire);
            var expectedType = kind switch
            {
                "waferGrade" => typeof(WaferGradePreview),
                "layerExposure" => typeof(LayerExposurePreview),
                "dicing" => typeof(DicingPreview),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
            Assert.AreEqual(expectedType, restored.GetType());
            var captured = JObject.Parse(restored.CaptureDrawRecord().ToString());
            Assert.IsTrue(JToken.DeepEquals(wire, captured));
            Assert.IsTrue(JToken.DeepEquals(wire, CleanRoomYieldPreviewResult.RestoreDrawRecord(captured).CaptureDrawRecord()));
        }

        [Test]
        public void DrawRecordRejectsUnknownKindsMissingRatesAndNonDrawStates()
        {
            Assert.IsNull(CleanRoomYieldPreviewResult.RestoreDrawRecord(null));
            Assert.Throws<InvalidOperationException>(() => CleanRoomYieldPreviewResult.RestoreDrawRecord(new JObject { ["kind"] = "unknown" }));
            Assert.Throws<InvalidOperationException>(() => CleanRoomYieldPreviewResult.RestoreDrawRecord(new JObject { ["kind"] = "dicing" }));
            Assert.Throws<InvalidOperationException>(() => NoRolePreview.Instance.CaptureDrawRecord());
            Assert.Throws<InvalidOperationException>(() => NotOperablePreview.Instance.CaptureDrawRecord());
        }
    }
}
