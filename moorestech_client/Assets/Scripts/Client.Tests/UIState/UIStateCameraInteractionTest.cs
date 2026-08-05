using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Client.Game.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.CameraPolicy;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Game.Skit;
using Client.Tests.ViewMode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.UIState
{
    public class UIStateCameraInteractionTest : InputTestFixture
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
        public void GameScreenAndBuildMenuPushTheirOnEnterPolicies()
        {
            SetUpGameStateController();
            var gameApplier = new FakePlayerCameraInteractionApplier();
            var gameState = new GameScreenState(null, null, null, null, CreateCameraPolicy(gameApplier));
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
            Press(_mouse.rightButton);
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
            Press(_mouse.rightButton);
            state.GetNextUpdate();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, applier.Calls);

            applier.Calls.Clear();
            state.OnExit();
            CollectionAssert.AreEqual(new[] { "Mode:PointerFree" }, applier.Calls);
        }

        private PlaceBlockState CreatePlaceBlockState(FakePlayerCameraInteractionApplier applier, FakeMapVeinRangeView mapVeinRangeView)
        {
            var skitManager = (SkitManager)FormatterServices.GetUninitializedObject(typeof(SkitManager));
            var dataStore = CreateComponent<BlockGameObjectDataStore>("BlockDataStore");
            var selector = new PlaceSystemSelector(null, null, null, null, null, null, null, null, null);
            var placeStateController = new PlaceSystemStateController(selector);
            var pickService = new PlacementTargetPickService(null);
            return new PlaceBlockState(skitManager, dataStore, placeStateController, pickService, CreateCameraPolicy(applier), new BuildUndoService(new BuildOperationHistory(), dataStore), mapVeinRangeView);
        }

        private static UiStateCameraPolicyService CreateCameraPolicy(FakePlayerCameraInteractionApplier applier)
        {
            return new UiStateCameraPolicyService(applier, new PlayerViewModeController(new FakePlayerViewApplier()));
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
