using Client.Input;
using Client.Tests.Common;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

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

            // 実actionからSendMessagesでOnSprintへ配送する
            // Deliver the real action to OnSprint through SendMessages
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
            _settings.Dispose();
            if (_hadMode) PlayerPrefs.SetString(SprintInputModePreference.PreferenceKey, _savedMode);
            else PlayerPrefs.DeleteKey(SprintInputModePreference.PreferenceKey);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
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
