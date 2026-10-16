using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Topics;
using Newtonsoft.Json.Linq;
using UnityEngine.TestTools;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Input;
using Client.Tests.Common;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Player
{
    public class SprintUiPolicyConnectionTest : InputTestFixture
    {
        private GameObject _root;
        private StarterAssetsInputs _inputs;
        private UIStateControl _ui;
        private bool _hadMode;
        private string _savedMode;

        public override void Setup()
        {
            base.Setup();
            _hadMode = PlayerPrefs.HasKey(SprintInputModePreference.PreferenceKey);
            _savedMode = PlayerPrefs.GetString(SprintInputModePreference.PreferenceKey);
            PlayerPrefs.DeleteKey(SprintInputModePreference.PreferenceKey);
            InputSystem.AddDevice<Keyboard>();
            TestReflection.ResetInputManagerCache();

            // 自機と入力の実接続を通す
            // Exercise the real player-to-input connection
            _root = new GameObject(nameof(SprintUiPolicyConnectionTest));
            _root.AddComponent<CharacterController>();
            _inputs = StarterAssetsInputTestLifecycle.AddInitializedInputs(_root);
            var movement = _root.AddComponent<ThirdPersonController>();
            var player = _root.AddComponent<PlayerObjectController>();
            TestReflection.SetField(player, "controller", movement);
            player.Initialize(Vector3.zero, Vector3.zero);

            // 移動可・走り反転不可の画面を宣言
            // Declare a screen allowing movement but disabling sprint toggling
            var dictionary = new UIStateDictionary(null, null, null, null, null, null, null, null, null, null, null, null);
            var states = TestReflection.GetField<Dictionary<UIStateEnum, IUIState>>(dictionary, "_stateDictionary");
            states[UIStateEnum.GameScreen] = new ScreenPolicy(true, null);
            states[UIStateEnum.SubInventory] = new ScreenPolicy(false, UIStateEnum.GameScreen);
            _ui = _root.AddComponent<UIStateControl>();
            _ui.Construct(dictionary, player);
            _ui.Initialize(UIStateEnum.GameScreen, new UITransitContext(UIStateEnum.GameScreen));
        }

        public override void TearDown()
        {
            StarterAssetsInputTestLifecycle.ReleaseInputs(_inputs);
            Object.DestroyImmediate(_root);
            SprintInputModePreference.SetMode(SprintInputMode.Toggle);
            if (_hadMode) PlayerPrefs.SetString(SprintInputModePreference.PreferenceKey, _savedMode);
            else PlayerPrefs.DeleteKey(SprintInputModePreference.PreferenceKey);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void 移動可能メニューでは反転せず閉鎖後は反転する()
        {
            _ui.RequestTransition(UIStateEnum.SubInventory);
            TestReflection.InvokePrivate(_ui, "Update");
            Assert.IsTrue(_inputs.inputEnable);
            _inputs.SprintInput(true);
            _inputs.SprintInput(false);
            Assert.IsTrue(_inputs.sprint);

            // 新しいボタンなしの自動閉鎖を通す
            // Close automatically without another button press
            TestReflection.InvokePrivate(_ui, "Update");
            Assert.AreEqual(UIStateEnum.GameScreen, _ui.CurrentState);
            _inputs.SprintInput(true);
            _inputs.SprintInput(false);
            Assert.IsFalse(_inputs.sprint);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 自動閉鎖をまたぐ押下は解放で反転しない(bool pressedBeforeMenu)
        {
            if (pressedBeforeMenu) _inputs.SprintInput(true);
            _ui.RequestTransition(UIStateEnum.SubInventory);
            TestReflection.InvokePrivate(_ui, "Update");
            if (!pressedBeforeMenu) _inputs.SprintInput(true);
            Assert.IsTrue(_inputs.inputEnable);
            TestReflection.InvokePrivate(_ui, "Update");
            Assert.AreEqual(UIStateEnum.GameScreen, _ui.CurrentState);
            _inputs.SprintInput(false);
            Assert.IsTrue(_inputs.sprint);
            _inputs.SprintInput(true);
            _inputs.SprintInput(false);
            Assert.IsFalse(_inputs.sprint);
        }

        [Test]
        public async Task 復旧通知だけを解除してもESC前の歩きを保持する()
        {
            PlayerPrefs.SetString(SprintInputModePreference.PreferenceKey, "2");
            LogAssert.Expect(LogType.Warning, new Regex("Unknown saved sprint mode"));
            Assert.AreEqual(SprintInputMode.Toggle, SprintInputModePreference.Current);
            var hub = new WebSocketHub();
            using var topic = new SprintInputModeTopic(hub);
            var recovered = JObject.Parse(await topic.GetSnapshotJsonAsync());
            Assert.IsTrue(recovered["recoveredInvalidPreference"].Value<bool>());

            // 歩きへ変えてからESC相当の入力無効を挟む
            // Start walking, then disable input as when opening ESC
            _inputs.SprintInput(true);
            _inputs.SprintInput(false);
            Assert.IsFalse(_inputs.sprint);
            _inputs.SetInputEnable(false);
            var revision = hub.GetTopicRevision(SprintInputModeTopic.TopicName);
            SprintInputModePreference.SetMode(SprintInputMode.Toggle);
            var acknowledged = JObject.Parse(await topic.GetSnapshotJsonAsync());
            Assert.IsFalse(acknowledged["recoveredInvalidPreference"].Value<bool>());
            Assert.AreEqual(revision + 1, hub.GetTopicRevision(SprintInputModeTopic.TopicName));
            _inputs.SetInputEnable(true);
            Assert.IsFalse(_inputs.sprint);
        }

        private sealed class ScreenPolicy : IUIState
        {
            private readonly bool _allowsSprintToggle;
            private readonly UIStateEnum? _automaticNextState;

            public ScreenPolicy(bool allowsSprintToggle, UIStateEnum? automaticNextState)
            {
                _allowsSprintToggle = allowsSprintToggle;
                _automaticNextState = automaticNextState;
            }

            public void OnEnter(UITransitContext context) { }
            public void OnExit() { }
            public bool LocksPlayerMovement() => false;
            public bool AllowsSprintToggle() => _allowsSprintToggle;
            public IReadOnlyList<KeyHint> GetKeyHints() => System.Array.Empty<KeyHint>();
            public UITransitContext GetNextUpdate() => _automaticNextState.HasValue
                ? new UITransitContext(_automaticNextState.Value) : null;
        }
    }
}
