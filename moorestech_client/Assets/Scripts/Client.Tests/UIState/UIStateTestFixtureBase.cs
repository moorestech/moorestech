using System.Collections.Generic;
using System.Reflection;
using Client.Game.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Interact.Selection;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.CameraPolicy;
using Client.Game.InGame.UI.UIState.State.CancelInput;
using Client.Game.InGame.UI.UIState.State.Hotbar;
using Client.Network.API;
using Server.Util.MessagePack;
using Client.Tests.UIState.Fakes;
using Client.Tests.ViewMode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     UIステートを直接組み立てるテストの共通土台
    ///     Shared base for tests that build UI states directly
    /// </summary>
    public abstract class UIStateTestFixtureBase : InputTestFixture
    {
        private readonly List<GameObject> _objects = new();
        private EventSystem _eventSystem;

        protected Mouse MouseDevice { get; private set; }
        protected Keyboard KeyboardDevice { get; private set; }

        public override void Setup()
        {
            base.Setup();
            MouseDevice = InputSystem.AddDevice<Mouse>();
            KeyboardDevice = InputSystem.AddDevice<Keyboard>();

            // UI上判定はEventSystem.currentを引くため、テスト毎に空のEventSystemを立てる
            // The over-UI check reads EventSystem.current, so give every test its own empty EventSystem
            _eventSystem = CreateObject("EventSystem", true).AddComponent<EventSystem>();
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

        // 装備はハイライト・遷移の検証に関与しないためnullで組む（採掘の可否判定まで踏み込むテストは別系統）
        // Equipment plays no part in highlight or transition checks, so it is built with null; mining outcome tests live elsewhere
        protected static InteractController CreateInteractController()
        {
            return new InteractController(null, new InteractTargetSelector());
        }

        protected static UiStateCameraPolicyService CreateCameraPolicy(FakePlayerCameraInteractionApplier applier)
        {
            return CreateCameraPolicy(applier, new PlayerViewModeController(new FakePlayerViewApplier()));
        }

        protected static UiStateCameraPolicyService CreateCameraPolicy(FakePlayerCameraInteractionApplier applier, PlayerViewModeController viewModeController)
        {
            return new UiStateCameraPolicyService(applier, viewModeController);
        }

        // 8px未満で押して離すだけの短押しをシミュレートする（移動なし）
        // Simulates a short press below the 8px threshold (press then release with no movement)
        protected UITransitContext PressAndReleaseRightButton(IUIState state)
        {
            Press(MouseDevice.rightButton);
            state.GetNextUpdate();
            Release(MouseDevice.rightButton);
            return state.GetNextUpdate();
        }

        // PlayerInventoryStateのctorは初期応答の適用まで走るため、必要な実体込みで組み立てる
        // PlayerInventoryState's ctor applies the initial response, so it is assembled together with the instances it needs
        protected PlayerInventoryState CreatePlayerInventoryState(LocalPlayerEquipment playerEquipment, InitialHandshakeResponse handshakeResponse)
        {
            return new PlayerInventoryState(
                new LocalPlayerInventoryController(new LocalPlayerInventory(), playerEquipment),
                playerEquipment, handshakeResponse, new RightShortPressInputService(new RightShortPressInput()));
        }

        // Handshakeは使用項目だけを設定する。残りはEditModeで組み立てられずPlayerInventoryStateも読まない
        // Only the fields the handshake consumes are set; the rest cannot be assembled in EditMode and PlayerInventoryState never reads them
        protected static InitialHandshakeResponse CreateHandshakeResponse(PlayerInventoryResponse inventory)
        {
#pragma warning disable CS0618
            var initialHandshake = new global::Server.Protocol.PacketResponse.InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack
            {
                PlayerPos = new Vector3MessagePack(Vector3.zero),
            };
#pragma warning restore CS0618

            return new InitialHandshakeResponse(initialHandshake, (null, null, inventory, null, null, null, null, null));
        }

        protected void SetUpMouseCursorTooltip()
        {
            var tooltip = CreateComponent<MouseCursorTooltip>("Tooltip", false);
            SetField(tooltip, "canvasGroup", tooltip.gameObject.AddComponent<CanvasGroup>());
            tooltip.gameObject.SetActive(true);
            InvokeAwake(tooltip);
        }

        // インタラクト選定は本番と同じUI重なり判定を通るため、Setupで立てたEventSystemを入力モジュール付きで有効化する
        // Interact selection runs the production UI-overlap check, so activate the Setup EventSystem with an input module
        protected void SetUpEventSystemInputModule()
        {
            _eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            InvokePrivate(_eventSystem, "OnEnable");
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

        protected static void InvokePrivate(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        protected static void InvokeAwake(object target)
        {
            InvokePrivate(target, "Awake");
        }
    }
}
