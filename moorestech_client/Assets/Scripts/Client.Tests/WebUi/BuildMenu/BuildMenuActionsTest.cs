using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Construction;
using Game.Construction;
using Game.PlacementTarget;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.UIState;
using Client.WebUiHost.Game.Actions;
using Client.WebUiHost.Game.Topics.BuildMenu;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.UnlockState;
using Game.UnlockState.States;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.WebUi
{
    public class BuildMenuActionsTest
    {
        [Test]
        public void BuildMenuSelectActionは現行カタログだけをBuildMenu中に選択する()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var catalog = new PlacementTargetCatalog(new BeltConveyorPlacementUnlockSourceMap());
            var unlocked = new AllPlacementTargetsUnlockedStateData();
            var controlObject = new GameObject("BuildMenuSelectActionTest.Control");
            var control = controlObject.AddComponent<UIStateControl>();
            var selection = new BuildMenuSelection();

            try
            {
                SetCurrentState(control, UIStateEnum.BuildMenu);
                var handler = new BuildMenuSelectActionHandler(
                    control, new PlacementTargetResolver(catalog, new ClientBlueprintLibrary(), unlocked), selection);
                var entry = catalog.UnlockedEntries(
                    unlocked, false, Array.Empty<(Guid, string)>()).First();

                var success = handler.ExecuteAsync(
                    new JObject { ["id"] = entry.Id.ToString("D") }).GetAwaiter().GetResult();
                Assert.IsTrue(success.Ok);
                Assert.IsTrue(selection.TryConsumeSelectedTarget(out var selected));
                Assert.AreEqual(entry.Id, selected.Id);

                var malformed = handler.ExecuteAsync(
                    new JObject { ["id"] = "not-a-guid" }).GetAwaiter().GetResult();
                Assert.AreEqual("invalid_payload", malformed.Error);

                SetCurrentState(control, UIStateEnum.GameScreen);
                var invalidState = handler.ExecuteAsync(
                    new JObject { ["id"] = entry.Id.ToString("D") }).GetAwaiter().GetResult();
                Assert.AreEqual("invalid_state", invalidState.Error);

                SetCurrentState(control, UIStateEnum.BuildMenu);
                var unknown = handler.ExecuteAsync(
                    new JObject { ["id"] = Guid.NewGuid().ToString("D") }).GetAwaiter().GetResult();
                Assert.AreEqual("unknown_entry", unknown.Error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controlObject);
            }

            #region Internal

            void SetCurrentState(UIStateControl control, UIStateEnum state)
            {
                typeof(UIStateControl).GetProperty(nameof(UIStateControl.CurrentState)).SetValue(control, state);
            }

            #endregion
        }

        [TestCase(BlueprintDeleteResult.Success, true, null)]
        [TestCase(BlueprintDeleteResult.NotFound, false, "blueprint_delete_not_found")]
        [TestCase(BlueprintDeleteResult.NotUnlocked, false, "blueprint_delete_not_unlocked")]
        [TestCase(BlueprintDeleteResult.RequestFailed, false, "blueprint_delete_request_failed")]
        [TestCase(BlueprintDeleteResult.Unknown, false, "blueprint_delete_unknown")]
        public void BlueprintDeleteActionは削除結果をエラー契約へ変換する(
            BlueprintDeleteResult deleteResult, bool expectedOk, string expectedError)
        {
            var service = new BlueprintDeleteServiceStub(deleteResult);
            var handler = new BlueprintDeleteActionHandler(service);
            var blueprintGuid = Guid.NewGuid();
            var result = handler.ExecuteAsync(
                new JObject { ["id"] = blueprintGuid.ToString("D") }).GetAwaiter().GetResult();
            Assert.AreEqual(expectedOk, result.Ok);
            Assert.AreEqual(expectedError, result.Error);
            Assert.AreEqual(blueprintGuid, service.LastBlueprintGuid);
        }

        private class BlueprintDeleteServiceStub : IBlueprintDeleteService
        {
            private readonly BlueprintDeleteResult _result;
            public Guid LastBlueprintGuid;

            public BlueprintDeleteServiceStub(BlueprintDeleteResult result) => _result = result;

            public UniTask<BlueprintDeleteResult> DeleteBlueprint(Guid blueprintGuid, CancellationToken ct)
            {
                LastBlueprintGuid = blueprintGuid;
                return UniTask.FromResult(_result);
            }
        }

        /// <summary>
        /// ブロック・車両・接続ツールを全解放するスタブ
        /// Stub that unlocks every block, train car, and connect tool
        /// </summary>
        private class AllPlacementTargetsUnlockedStateData : IGameUnlockStateData
        {
            public IReadOnlyDictionary<Guid, BlockUnlockStateInfo> BlockUnlockStateInfos { get; } =
                MasterHolder.BlockMaster.Blocks.Data.ToDictionary(b => b.BlockGuid, b => new BlockUnlockStateInfo(b.BlockGuid, true));

            public IReadOnlyDictionary<Guid, ConnectToolUnlockStateInfo> ConnectToolUnlockStateInfos { get; } =
                MasterHolder.ConnectToolMaster.All.ToDictionary(c => c.ConnectToolGuid, c => new ConnectToolUnlockStateInfo(c.ConnectToolGuid, true));

            public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos { get; } = new Dictionary<Guid, CraftRecipeUnlockStateInfo>();
            public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos { get; } = new Dictionary<ItemId, ItemUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> ChallengeCategoryUnlockStateInfos { get; } = new Dictionary<Guid, ChallengeCategoryUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> MachineRecipeUnlockStateInfos { get; } = new Dictionary<Guid, MachineRecipeUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, TrainCarUnlockStateInfo> TrainCarUnlockStateInfos { get; } =
                MasterHolder.TrainUnitMaster.Train.TrainCars.ToDictionary(
                    trainCar => trainCar.TrainCarGuid,
                    trainCar => new TrainCarUnlockStateInfo(trainCar.TrainCarGuid, true));

            public bool IsBlueprintUnlocked => true;
        }
    }
}
