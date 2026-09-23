using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Tests.Common;
using Client.Tests.PlaceSystem.TrainCostIntegration;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Placement;
using Game.Blueprint;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
namespace Client.Tests.PlaceSystem.Blueprint
{
    public sealed class BlueprintPasteDirectionDiagnosticTest : TrainPlacementEntryFixture
    {
        [Test]
        public void ManualUpdateReportsOnlyExplicitPasteAttempts()
        {
            var id = ForUnitTestModBlockId.GearBeltConveyor;
            var validator = new BlockPlacementValidation(new IBlockPlacementValidator[] { new BeltPlacementValidator() });
            var store = CreateObject("BlockStore").AddComponent<BlockGameObjectDataStore>();
            var system = new BlueprintPasteSystem(PlacementCamera, null, store, validator);
            system.Enable();
            var preview = TestReflection.GetField<BlueprintPastePreviewController>(system, "_previewController");
            var pool = TestReflection.GetField<BlockPlacePreviewObjectPool>(preview, "_pool");
            var previewRoot = TestReflection.GetField<Transform>(pool, "_parentTransform");
            previewRoot.SetParent(Root.transform);
            // 実プレビューpoolにテスト用の空モデルを置き、assetロードだけを省く。
            // Put an empty model in the real preview pool, bypassing only asset loading.
            var visual = CreateObject("Preview").AddComponent<BlockPreviewObject>(); visual.Initialize(id);
            var entryType = typeof(BlockPlacePreviewObjectPool).GetNestedType("PreviewObject", BindingFlags.NonPublic);
            var entry = Activator.CreateInstance(entryType);
            entryType.GetField("BlockPreviewObject").SetValue(entry, visual);
            var entries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType)); entries.Add(entry);
            var map = (IDictionary)typeof(BlockPlacePreviewObjectPool).GetField("_blockPreviewObjects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pool);
            map.Add(id, entries);
            var blueprint = new BlueprintJsonObject("direction", new List<BlueprintBlockJsonObject>
            { new(Vector3Int.zero, MasterHolder.BlockMaster.GetBlockMaster(id).BlockGuid.ToString(), (int)BlockDirection.UpNorth, new()) }, Guid.NewGuid());
            TestReflection.SetField(system, "_currentBlueprint", blueprint);
            var context = new PlaceSystemUpdateContext(new BlueprintPlacementTarget(blueprint.BlueprintGuid, blueprint.Name), false, new PlacementFeedback());
            int warnings = 0; Application.logMessageReceived += CountWarning;
            try
            {
                for (int frame = 0; frame < 3; frame++) { InputSystem.Update(); system.ManualUpdate(context); }
                Assert.AreEqual(0, warnings);
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    ClickRelease();
                    LogAssert.Expect(LogType.Warning, new Regex("^\\[BlueprintPaste\\] Rejected belt direction:"));
                    system.ManualUpdate(context);
                }
                Assert.AreEqual(2, warnings); AssertNoRequest();
            }
            finally { Application.logMessageReceived -= CountWarning; system.Disable(); }
            #region Internal
            void CountWarning(string message, string stack, LogType type)
            { if (message.StartsWith("[BlueprintPaste] Rejected belt direction:")) warnings++; }
            #endregion
        }
    }
}
