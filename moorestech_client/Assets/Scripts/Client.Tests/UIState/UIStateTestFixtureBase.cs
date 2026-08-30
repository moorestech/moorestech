using System.Collections.Generic;
using System.Reflection;
using Client.Game.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState.State.CameraPolicy;
using Client.Game.InGame.UI.UIState.State.Hotbar;
using Client.Tests.UIState.Fakes;
using Client.Tests.ViewMode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     UIステートを直接組み立てるテストの共通土台
    ///     Shared base for tests that build UI states directly
    /// </summary>
    public abstract class UIStateTestFixtureBase : InputTestFixture
    {
        private readonly List<GameObject> _objects = new();

        protected Mouse MouseDevice { get; private set; }
        protected Keyboard KeyboardDevice { get; private set; }

        public override void Setup()
        {
            base.Setup();
            MouseDevice = InputSystem.AddDevice<Mouse>();
            KeyboardDevice = InputSystem.AddDevice<Keyboard>();

            // UI上判定はEventSystem.currentを引くため、テスト毎に空のEventSystemを立てる
            // The over-UI check reads EventSystem.current, so give every test its own empty EventSystem
            CreateObject("EventSystem", true).AddComponent<EventSystem>();
        }

        public override void TearDown()
        {
            foreach (var gameObject in _objects)
                if (gameObject != null) Object.DestroyImmediate(gameObject);
            _objects.Clear();

            // 静的な照準状態を持ち越さない
            // Reset the static aim state so nothing leaks across tests
            AimPointProvider.SetViewMode(PlayerViewMode.ThirdPerson);
            AimPointProvider.SetThirdPersonAimSource(ThirdPersonAimSource.ScreenCenter);
            base.TearDown();
        }

        // 数字キー状態はサービス経由でしか触れないため、テストも本番と同じ組み立てで生成する
        // The digit-key state is reachable only through the service, so tests build it the same way production does
        protected static HotbarTapInputService CreateHotbarTapInputService(PlaceSystemStateController placeStateController)
        {
            return new HotbarTapInputService(new ClientHotbarDatastore(), null, placeStateController, new HotbarKeyInput());
        }

        protected static UiStateCameraPolicyService CreateCameraPolicy(FakePlayerCameraInteractionApplier applier)
        {
            return CreateCameraPolicy(applier, new PlayerViewModeController(new FakePlayerViewApplier()));
        }

        protected static UiStateCameraPolicyService CreateCameraPolicy(FakePlayerCameraInteractionApplier applier, PlayerViewModeController viewModeController)
        {
            return new UiStateCameraPolicyService(applier, viewModeController);
        }

        protected void SetUpMouseCursorTooltip()
        {
            var tooltip = CreateComponent<MouseCursorTooltip>("Tooltip", false);
            SetField(tooltip, "canvasGroup", tooltip.gameObject.AddComponent<CanvasGroup>());
            tooltip.gameObject.SetActive(true);
            InvokeAwake(tooltip);
        }

        protected void SetUpGameStateController()
        {
            var playerRoot = CreateObject("PlayerSystem", false);
            var grabManager = playerRoot.AddComponent<PlayerGrabItemManager>();
            var playerController = playerRoot.AddComponent<PlayerObjectController>();
            var playerContainer = playerRoot.AddComponent<PlayerSystemContainer>();
            SetField(playerContainer, "playerGrabItemManager", grabManager);
            SetField(playerContainer, "playerObjectController", playerController);
            playerRoot.SetActive(true);
            InvokeAwake(playerContainer);

            var challengeHud = CreateComponent<CurrentChallengeHudView>("ChallengeHud");
            var gameState = CreateComponent<GameStateController>("GameState", false);
            SetField(gameState, "currentChallengeHudView", challengeHud);
            gameState.gameObject.SetActive(true);
            InvokeAwake(gameState);
        }

        protected T CreateComponent<T>(string name) where T : Component
        {
            return CreateComponent<T>(name, true);
        }

        protected T CreateComponent<T>(string name, bool active) where T : Component
        {
            var gameObject = CreateObject(name, active);
            return gameObject.AddComponent<T>();
        }

        protected GameObject CreateObject(string name, bool active)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(active);
            _objects.Add(gameObject);
            return gameObject;
        }

        protected static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        protected static void InvokeAwake(object target)
        {
            target.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }
    }
}
