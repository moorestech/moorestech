using Client.Input;
using Client.Tests.Common;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Client.Tests.Player
{
    public class SprintAnalogInputTest : InputTestFixture
    {
        private GameObject _root;
        private StarterAssetsInputs _inputs;
        private PlayerInput _playerInput;
        private MoorestechInputSettings _settings;
        private Gamepad _gamepad;
        private bool _hadMode;
        private string _savedMode;

        public override void Setup()
        {
            base.Setup();
            _hadMode = PlayerPrefs.HasKey(SprintInputModePreference.PreferenceKey);
            _savedMode = PlayerPrefs.GetString(SprintInputModePreference.PreferenceKey);
            PlayerPrefs.DeleteKey(SprintInputModePreference.PreferenceKey);
            _gamepad = InputSystem.AddDevice<Gamepad>();
            TestReflection.ResetInputManagerCache();
            _root = new GameObject(nameof(SprintAnalogInputTest));
            _inputs = StarterAssetsInputTestLifecycle.AddInitializedInputs(_root);
            _inputs.runInEditMode = true;

            // 実入力をメッセージ経由で配送
            // Deliver real input through SendMessages
            _settings = new MoorestechInputSettings();
            _playerInput = _root.AddComponent<PlayerInput>();
            _playerInput.notificationBehavior = PlayerNotifications.SendMessages;
            _playerInput.actions = _settings.asset;
            _playerInput.defaultActionMap = "Player";
            TestReflection.InvokePrivate(_playerInput, "OnEnable");
            _playerInput.ActivateInput();
        }

        public override void TearDown()
        {
            TestReflection.InvokePrivate(_playerInput, "OnDisable");
            StarterAssetsInputTestLifecycle.ReleaseInputs(_inputs);
            Object.DestroyImmediate(_root);
            _settings.asset.Disable();
            Object.DestroyImmediate(_settings.asset);
            if (_hadMode) PlayerPrefs.SetString(SprintInputModePreference.PreferenceKey, _savedMode);
            else PlayerPrefs.DeleteKey(SprintInputModePreference.PreferenceKey);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void 実PlayerInputで単独と先行キーと同時押下を区別する(bool precedingW, bool simultaneousR)
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            // 新しいデバイスへ実PlayerInputの配送先を切り替える
            // Route real PlayerInput delivery to the new device
            var mouse = InputSystem.AddDevice<Mouse>();
            _playerInput.SwitchCurrentControlScheme("KeyboardMouse", keyboard, mouse);
            Assert.AreEqual("KeyboardMouse", _playerInput.currentControlScheme);
            if (precedingW) Press(keyboard.wKey);
            var pressed = simultaneousR ? new KeyboardState(Key.LeftShift, Key.R)
                : precedingW ? new KeyboardState(Key.W, Key.LeftShift) : new KeyboardState(Key.LeftShift);
            InputSystem.QueueStateEvent(keyboard, pressed);
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, precedingW ? new KeyboardState(Key.W) : new KeyboardState());
            InputSystem.Update();
            Assert.AreEqual(simultaneousR, _inputs.sprint);
        }

        [Test]
        public void アナログ継続通知は別ボタンによる取消を戻さない()
        {
            // 単独操作で実配送が生きていることも検証する
            // Verify delivery is live with an isolated operation first
            Set(_gamepad.leftTrigger, 0.7f);
            Set(_gamepad.leftTrigger, 0f);
            Assert.IsFalse(_inputs.sprint);
            Set(_gamepad.leftTrigger, 0.7f);
            Press(_gamepad.buttonSouth);
            Set(_gamepad.leftTrigger, 0.9f);
            Set(_gamepad.leftTrigger, 0f);
            Assert.IsFalse(_inputs.sprint);
        }
    }
}
