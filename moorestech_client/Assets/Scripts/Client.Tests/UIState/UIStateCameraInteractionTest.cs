using System.Runtime.Serialization;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Game.Skit;
using Client.Tests.UIState.Fakes;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    public class UIStateCameraInteractionTest : UIStateTestFixtureBase
    {
        [Test]
        public void GameScreenAndBuildMenuPushTheirOnEnterPolicies()
        {
            SetUpGameStateController();
            var gameApplier = new FakePlayerCameraInteractionApplier();
            var gameState = new GameScreenState(null, null, null, null, CreateCameraPolicy(gameApplier), CreateHotbarTapInputService(null));
            gameState.OnEnter(new UITransitContext(UIStateEnum.GameScreen));
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, gameApplier.Calls);

            var menuApplier = new FakePlayerCameraInteractionApplier();
            var menuView = new FakeBuildMenuView();
            var menuState = new BuildMenuState(menuView, CreateCameraPolicy(menuApplier));
            menuState.OnEnter(new UITransitContext(UIStateEnum.BuildMenu));
            CollectionAssert.AreEqual(new[] { "Mode:PointerFree" }, menuApplier.Calls);
            Assert.IsTrue(menuView.IsActive);
        }

        [Test]
        public void PlaceBlockPushesEnterDragStartAndExitPolicies()
        {
            var applier = new FakePlayerCameraInteractionApplier();
            var state = CreatePlaceBlockState(applier, new FakeMapVeinRangeView());
            state.OnEnter(new UITransitContext(UIStateEnum.PlaceBlock));
            CollectionAssert.AreEqual(new[] { "Mode:PointerFree" }, applier.Calls);

            // ドラッグ全遷移はUiStateCameraPolicyServiceTest側が担い、ここでは委譲配線のみ確認
            // Full drag transitions are covered by UiStateCameraPolicyServiceTest; only the delegation wiring is verified here
            applier.Calls.Clear();
            Press(MouseDevice.rightButton);
            state.GetNextUpdate();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, applier.Calls);

            applier.Calls.Clear();
            state.OnExit();
            CollectionAssert.AreEqual(new[] { "Mode:PointerFree" }, applier.Calls);
        }

        [Test]
        public void PlaceBlockPushesVeinRangeVisibilityOnlyOnEnterAndExit()
        {
            var mapVeinRangeView = new FakeMapVeinRangeView();
            var state = CreatePlaceBlockState(new FakePlayerCameraInteractionApplier(), mapVeinRangeView);

            state.OnEnter(new UITransitContext(UIStateEnum.PlaceBlock));
            CollectionAssert.AreEqual(new[] { true }, mapVeinRangeView.ShowPushes);

            // 表示ON/OFFは変化時だけプッシュし、毎フレームはカメラ距離カリングのManualUpdateだけを回す
            // Visibility is pushed only on change; each frame drives just ManualUpdate for the camera distance culling
            for (var frame = 0; frame < 3; frame++) state.GetNextUpdate();
            CollectionAssert.AreEqual(new[] { true }, mapVeinRangeView.ShowPushes);
            Assert.AreEqual(3, mapVeinRangeView.ManualUpdateCount);

            state.OnExit();
            CollectionAssert.AreEqual(new[] { true, false }, mapVeinRangeView.ShowPushes);
        }

        [Test]
        public void DeleteObjectPushesEnterDragStartAndExitPolicies()
        {
            SetUpMouseCursorTooltip();
            var deleteObject = CreateComponent<DeleteBarObject>("DeleteBar");
            var applier = new FakePlayerCameraInteractionApplier();
            // 履歴はサービスと共有する（記録先とpop元が別インスタンスになる罠の防止）
            // Share the history with the service (avoids the trap of recording into a different instance than the one popped)
            var buildOperationHistory = new BuildOperationHistory();
            var state = new DeleteObjectState(deleteObject, null, CreateCameraPolicy(applier), buildOperationHistory, new BuildUndoService(buildOperationHistory, null));
            state.OnEnter(new UITransitContext(UIStateEnum.DeleteBar));
            CollectionAssert.AreEqual(new[] { "Mode:PointerFree" }, applier.Calls);

            applier.Calls.Clear();
            Press(MouseDevice.rightButton);
            state.GetNextUpdate();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, applier.Calls);

            applier.Calls.Clear();
            state.OnExit();
            CollectionAssert.AreEqual(new[] { "Mode:PointerFree" }, applier.Calls);
        }

        [Test]
        public void GameScreenDelegatesLeftAltFreeCursorToPolicyService()
        {
            SetUpGameStateController();
            var applier = new FakePlayerCameraInteractionApplier();
            var state = CreateGameScreenState(applier);
            state.OnEnter(new UITransitContext(UIStateEnum.GameScreen));

            // 左Alt押下がサービスへ届くことだけ見る
            // Verify only that the left Alt press reaches the service
            applier.Calls.Clear();
            Press(KeyboardDevice.leftAltKey);
            state.GetNextUpdate();
            CollectionAssert.AreEqual(new[] { "Mode:PointerFree", "Warp" }, applier.Calls);

            applier.Calls.Clear();
            Release(KeyboardDevice.leftAltKey);
            state.GetNextUpdate();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, applier.Calls);
        }

        private GameScreenState CreateGameScreenState(FakePlayerCameraInteractionApplier applier)
        {
            var skitManager = (SkitManager)FormatterServices.GetUninitializedObject(typeof(SkitManager));
            var subInventoryInteractService = new GameScreenSubInventoryInteractService(null);
            var rideVehicleInputService = new RideVehicleInputService();
            var placementTargetPickService = new PlacementTargetPickService(null);
            return new GameScreenState(skitManager, subInventoryInteractService, rideVehicleInputService, placementTargetPickService, CreateCameraPolicy(applier), CreateHotbarTapInputService(null));
        }

        private PlaceBlockState CreatePlaceBlockState(FakePlayerCameraInteractionApplier applier, FakeMapVeinRangeView mapVeinRangeView)
        {
            var skitManager = (SkitManager)FormatterServices.GetUninitializedObject(typeof(SkitManager));
            var dataStore = CreateComponent<BlockGameObjectDataStore>("BlockDataStore");
            var selector = new PlaceSystemSelector(null, null, null, null, null, null, null, null, null);
            var placeStateController = new PlaceSystemStateController(selector, new PlacementFeedbackTooltipPresenter());
            var pickService = new PlacementTargetPickService(null);
            var hotbarInputService = CreateHotbarTapInputService(placeStateController);
            return new PlaceBlockState(skitManager, dataStore, placeStateController, pickService, CreateCameraPolicy(applier), new BuildUndoService(new BuildOperationHistory(), dataStore), mapVeinRangeView, hotbarInputService);
        }
    }
}
