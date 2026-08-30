using System.Runtime.Serialization;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Interact;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Game.Skit;
using Client.Game.InGame.Map.MapVein;
using Client.Tests.Map.Vein;
using Client.Tests.UIState.Fakes;
using Client.Tests.ViewMode;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    public class UIStateFocusRestorationTest : UIStateTestFixtureBase
    {
        [Test]
        public void GameScreenExitReturnsToNeutralBeforeDirectInventoryTransition()
        {
            SetUpGameStateController();
            SetUpMouseCursorTooltip();
            var applier = new FakePlayerCameraInteractionApplier();
            var state = new GameScreenState(null, CreateInteractController(), null, CreateCameraPolicy(applier), CreateHotbarTapInputService(null));
            state.OnEnter(new UITransitContext(UIStateEnum.GameScreen));

            applier.Calls.Clear();
            state.OnExit();

            CollectionAssert.AreEqual(new[] { "Mode:PointerFree" }, applier.Calls);
        }

        [Test]
        public void PlaceBlockRestoresBaselineAfterFocusReturnsDuringRightDrag()
        {
            var applier = new FakePlayerCameraInteractionApplier();
            var state = CreatePlaceBlockState(applier, new PlayerViewModeController(new FakePlayerViewApplier()));
            state.OnEnter(new UITransitContext(UIStateEnum.PlaceBlock));
            Press(MouseDevice.rightButton);
            state.GetNextUpdate();

            applier.Calls.Clear();
            state.RestoreAfterApplicationFocus();

            CollectionAssert.AreEqual(new[] { "Mode:PointerFree" }, applier.Calls);
        }

        [Test]
        public void DeleteObjectRestoresBaselineAfterFocusReturnsDuringRightDrag()
        {
            SetUpMouseCursorTooltip();
            var applier = new FakePlayerCameraInteractionApplier();
            var state = CreateDeleteObjectState(applier, new PlayerViewModeController(new FakePlayerViewApplier()));
            state.OnEnter(new UITransitContext(UIStateEnum.DeleteBar));
            Press(MouseDevice.rightButton);
            state.GetNextUpdate();

            applier.Calls.Clear();
            state.RestoreAfterApplicationFocus();

            CollectionAssert.AreEqual(new[] { "Mode:PointerFree" }, applier.Calls);
        }

        [Test]
        public void PlaceBlockKeepsFpsControlOnEnterAndFocusRestore()
        {
            var applier = new FakePlayerCameraInteractionApplier();
            var viewModeController = new PlayerViewModeController(new FakePlayerViewApplier());
            viewModeController.ToggleViewMode();
            var state = CreatePlaceBlockState(applier, viewModeController);
            state.OnEnter(new UITransitContext(UIStateEnum.PlaceBlock));
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, applier.Calls);

            applier.Calls.Clear();
            state.RestoreAfterApplicationFocus();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, applier.Calls);
        }

        [Test]
        public void DeleteObjectKeepsFpsControlOnEnterAndFocusRestore()
        {
            var applier = new FakePlayerCameraInteractionApplier();
            var viewModeController = new PlayerViewModeController(new FakePlayerViewApplier());
            viewModeController.ToggleViewMode();
            var state = CreateDeleteObjectState(applier, viewModeController);
            state.OnEnter(new UITransitContext(UIStateEnum.DeleteBar));
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, applier.Calls);

            applier.Calls.Clear();
            state.RestoreAfterApplicationFocus();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, applier.Calls);
        }

        private PlaceBlockState CreatePlaceBlockState(FakePlayerCameraInteractionApplier applier, PlayerViewModeController viewModeController)
        {
            var skitManager = (SkitManager)FormatterServices.GetUninitializedObject(typeof(SkitManager));
            var dataStore = CreateComponent<BlockGameObjectDataStore>("BlockDataStore");
            var selector = new PlaceSystemSelector(null, null, null, null, null, null, null, null, null);
            var placeStateController = new PlaceSystemStateController(selector, new PlacementFeedbackTooltipPresenter());
            var pickService = new PlacementTargetPickService(null);
            var hotbarInputService = CreateHotbarTapInputService(placeStateController);
            return new PlaceBlockState(skitManager, dataStore, placeStateController, pickService, CreateCameraPolicy(applier, viewModeController), new BuildUndoService(new BuildOperationHistory(), dataStore), new FakeMapVeinRangeView(), CreateVeinAabbRegistry(), new VeinRestrictedPlacementState(), hotbarInputService);
        }

        private DeleteObjectState CreateDeleteObjectState(FakePlayerCameraInteractionApplier applier, PlayerViewModeController viewModeController)
        {
            var deleteObject = CreateComponent<DeleteBarObject>("DeleteBar");
            // 履歴はサービスと共有する（記録先とpop元が別インスタンスになる罠の防止）
            // Share the history with the service (avoids the trap of recording into a different instance than the one popped)
            var buildOperationHistory = new BuildOperationHistory();
            return new DeleteObjectState(deleteObject, null, CreateCameraPolicy(applier, viewModeController), buildOperationHistory, new BuildUndoService(buildOperationHistory, null), new PlacementTargetPickService(null));
        }

        // 鉱脈ゼロの台帳。PlaceBlockStateはコンストラクタで表示を解決するため実体が要る
        // A registry with no veins; PlaceBlockState resolves the display in its constructor and needs a real one
        private static MapVeinAabbRegistry CreateVeinAabbRegistry()
        {
            return MapVeinAabbRegistryFixture.Create();
        }
    }
}
