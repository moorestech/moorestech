using System.Runtime.Serialization;
using Client.Game.InGame.Block;
using Client.Game.InGame.Map.MapVein;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Game.Skit;
using Client.Tests.Map.Vein;
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
        public void PlaceBlockPushesVeinDisplayOnlyOnTargetChange()
        {
            var mapVeinRangeView = new FakeMapVeinRangeView();
            var state = CreatePlaceBlockState(new FakePlayerCameraInteractionApplier(), mapVeinRangeView);

            // 設置対象を載せない遷移では表示種別も変わらない。滞在するだけでは何もプッシュしない
            // A transition without a placement target changes no vein kind; merely entering pushes nothing
            state.OnEnter(new UITransitContext(UIStateEnum.PlaceBlock));
            CollectionAssert.IsEmpty(mapVeinRangeView.DisplayPushes);

            // 表示種別は対象変化時だけプッシュし、毎フレームはカメラ距離カリングのManualUpdateだけを回す
            // The vein kind is pushed only when the target changes; each frame drives just ManualUpdate for the camera distance culling
            for (var frame = 0; frame < 3; frame++) state.GetNextUpdate();
            CollectionAssert.IsEmpty(mapVeinRangeView.DisplayPushes);
            Assert.AreEqual(3, mapVeinRangeView.ManualUpdateCount);

            // 離脱は対象がnullになる通知経由で畳む
            // Leaving folds the view through the null-target notification
            state.OnExit();
            CollectionAssert.AreEqual(new[] { VeinDisplay.Hidden }, mapVeinRangeView.DisplayPushes);
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
            var state = new DeleteObjectState(deleteObject, null, CreateCameraPolicy(applier), buildOperationHistory, new BuildUndoService(buildOperationHistory, null), new PlacementTargetPickService(null));
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
            return new PlaceBlockState(skitManager, dataStore, placeStateController, pickService, CreateCameraPolicy(applier), new BuildUndoService(new BuildOperationHistory(), dataStore), mapVeinRangeView, CreateVeinAabbRegistry(), new VeinRestrictedPlacementState(), hotbarInputService);
        }

        // 鉱脈ゼロの台帳。PlaceBlockStateはコンストラクタで表示を解決するため実体が要る
        // A registry with no veins; PlaceBlockState resolves the display in its constructor and needs a real one
        private static MapVeinAabbRegistry CreateVeinAabbRegistry()
        {
            return MapVeinAabbRegistryFixture.Create();
        }
    }
}
