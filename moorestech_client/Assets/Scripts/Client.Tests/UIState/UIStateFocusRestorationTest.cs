using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Client.Game.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.CameraPolicy;
using Client.Game.InGame.UI.UIState.State.Hotbar;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Game.Skit;
using Client.Tests.ViewMode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.UIState
{
    public class UIStateFocusRestorationTest : InputTestFixture
    {
        private readonly List<GameObject> _objects = new();
        private Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            InvokeAwake(CreateComponent<KeyControlDescription>("KeyControl"));
        }

        public override void TearDown()
        {
            foreach (var gameObject in _objects)
                if (gameObject != null) Object.DestroyImmediate(gameObject);
            _objects.Clear();
            base.TearDown();
        }

        [Test]
        public void GameScreenExitReturnsToNeutralBeforeDirectInventoryTransition()
        {
            SetUpGameStateController();
            var applier = new FakePlayerCameraInteractionApplier();
            var state = new GameScreenState(null, null, null, null, CreateCameraPolicy(applier, new PlayerViewModeController(new FakePlayerViewApplier())), null, null);
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
            Press(_mouse.rightButton);
            state.GetNextUpdate();

            applier.Calls.Clear();
            state.RestoreAfterApplicationFocus();

            CollectionAssert.AreEqual(new[] { "Mode:PointerFree" }, applier.Calls);
        }

        [Test]
        public void DeleteObjectRestoresBaselineAfterFocusReturnsDuringRightDrag()
        {
            SetUpMouseCursorTooltip();
            var deleteObject = CreateComponent<DeleteBarObject>("DeleteBar");
            var applier = new FakePlayerCameraInteractionApplier();
            // 履歴はサービスと共有する（記録先とpop元が別インスタンスになる罠の防止）
            // Share the history with the service (avoids the trap of recording into a different instance than the one popped)
            var buildOperationHistory = new BuildOperationHistory();
            var state = new DeleteObjectState(deleteObject, null, CreateCameraPolicy(applier, new PlayerViewModeController(new FakePlayerViewApplier())), buildOperationHistory, new BuildUndoService(buildOperationHistory, null));
            state.OnEnter(new UITransitContext(UIStateEnum.DeleteBar));
            Press(_mouse.rightButton);
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
            var deleteObject = CreateComponent<DeleteBarObject>("DeleteBar");
            var applier = new FakePlayerCameraInteractionApplier();
            var viewModeController = new PlayerViewModeController(new FakePlayerViewApplier());
            viewModeController.ToggleViewMode();
            var buildOperationHistory = new BuildOperationHistory();
            var state = new DeleteObjectState(deleteObject, null, CreateCameraPolicy(applier, viewModeController), buildOperationHistory, new BuildUndoService(buildOperationHistory, null));
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
            var placeStateController = new PlaceSystemStateController(selector);
            var pickService = new PlacementTargetPickService(null);
            var clientHotbarDatastore = new ClientHotbarDatastore();
            var hotbarInputService = new PlaceBlockHotbarInputService(clientHotbarDatastore, null, placeStateController);
            return new PlaceBlockState(skitManager, dataStore, placeStateController, pickService, CreateCameraPolicy(applier, viewModeController), new BuildUndoService(new BuildOperationHistory(), dataStore), new FakeMapVeinRangeView(), clientHotbarDatastore, hotbarInputService);
        }

        private static UiStateCameraPolicyService CreateCameraPolicy(FakePlayerCameraInteractionApplier applier, PlayerViewModeController viewModeController)
        {
            return new UiStateCameraPolicyService(applier, viewModeController);
        }

        private void SetUpMouseCursorTooltip()
        {
            var tooltip = CreateComponent<MouseCursorTooltip>("Tooltip", false);
            SetField(tooltip, "canvasGroup", tooltip.gameObject.AddComponent<CanvasGroup>());
            tooltip.gameObject.SetActive(true);
            InvokeAwake(tooltip);
        }

        private void SetUpGameStateController()
        {
            var playerRoot = CreateObject("PlayerSystem", false);
            var grabManager = playerRoot.AddComponent<PlayerGrabItemManager>();
            var playerController = playerRoot.AddComponent<PlayerObjectController>();
            var playerContainer = playerRoot.AddComponent<PlayerSystemContainer>();
            SetField(playerContainer, "playerGrabItemManager", grabManager);
            SetField(playerContainer, "playerObjectController", playerController);
            playerRoot.SetActive(true);
            InvokeAwake(playerContainer);

            var hotBar = CreateComponent<HotBarView>("HotBar");
            var challengeHud = CreateComponent<CurrentChallengeHudView>("ChallengeHud");
            var gameState = CreateComponent<GameStateController>("GameState", false);
            SetField(gameState, "currentChallengeHudView", challengeHud);
            gameState.Construct(hotBar);
            gameState.gameObject.SetActive(true);
            InvokeAwake(gameState);
        }

        private T CreateComponent<T>(string name) where T : Component
        {
            return CreateComponent<T>(name, true);
        }

        private T CreateComponent<T>(string name, bool active) where T : Component
        {
            var gameObject = CreateObject(name, active);
            return gameObject.AddComponent<T>();
        }

        private GameObject CreateObject(string name, bool active)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(active);
            _objects.Add(gameObject);
            return gameObject;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static void InvokeAwake(object target)
        {
            target.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }
    }
}
