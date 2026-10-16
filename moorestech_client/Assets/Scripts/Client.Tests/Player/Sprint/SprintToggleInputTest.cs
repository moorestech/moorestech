using Client.Input;
using Client.Tests.Common;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Player
{
    public class SprintToggleInputTest : InputTestFixture
    {
        private GameObject _root;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private StarterAssetsInputs _inputs;
        private bool _hadSavedMode;
        private string _savedMode;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            TestReflection.ResetInputManagerCache();
            _ = InputManager.Player;
            InputSystem.Update();
            // 開発者自身の設定を壊さないよう退避してから既定（未保存）にする
            // Stash the developer's own setting, then start from the unsaved default
            _hadSavedMode = PlayerPrefs.HasKey(SprintInputModePreference.PreferenceKey);
            _savedMode = PlayerPrefs.GetString(SprintInputModePreference.PreferenceKey);
            PlayerPrefs.DeleteKey(SprintInputModePreference.PreferenceKey);

            _root = new GameObject("SprintToggleInputTest");
            _inputs = StarterAssetsInputTestLifecycle.AddInitializedInputs(_root);
        }

        public override void TearDown()
        {
            StarterAssetsInputTestLifecycle.ReleaseInputs(_inputs);
            Object.DestroyImmediate(_root);
            if (_hadSavedMode) PlayerPrefs.SetString(SprintInputModePreference.PreferenceKey, _savedMode);
            else PlayerPrefs.DeleteKey(SprintInputModePreference.PreferenceKey);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void 非アクティブ期間をまたぐ押下は反転しない()
        {
            Press(_keyboard.leftShiftKey);
            _inputs.SprintInput(true);
            // EditModeでは無効化と復帰の通知を明示する
            // Explicitly deliver disable and enable callbacks in EditMode
            TestReflection.InvokePrivate(_inputs, "OnDisable");
            _root.SetActive(false);
            Press(_keyboard.rKey);
            _root.SetActive(true);
            TestReflection.InvokePrivate(_inputs, "OnEnable");
            Release(_keyboard.leftShiftKey);
            _inputs.SprintInput(false);
            Assert.IsTrue(_inputs.sprint);
            _inputs.SprintInput(true);
            _inputs.SprintInput(false);
            Assert.IsFalse(_inputs.sprint);
        }

        [Test]
        public void 既定トグルで開始時から走りShift単独タップで歩きになる()
        {
            Assert.IsTrue(_inputs.sprint);
            Press(_keyboard.leftShiftKey);
            _inputs.SprintInput(true);
            Release(_keyboard.leftShiftKey);
            _inputs.SprintInput(false);
            Assert.IsFalse(_inputs.sprint);
        }

        [Test]
        public void Wを押したままShift単独タップで反転する()
        {
            // 先に押されていたキーは修飾判定に数えない（押下〜離しの間に押されたボタンだけを見る）
            Press(_keyboard.wKey);
            Press(_keyboard.leftShiftKey);
            _inputs.SprintInput(true);
            Release(_keyboard.leftShiftKey);
            _inputs.SprintInput(false);
            Assert.IsFalse(_inputs.sprint);
        }

        [Test]
        public void Shift押下中にWを押し始めたら反転しない()
        {
            Press(_keyboard.leftShiftKey);
            _inputs.SprintInput(true);
            Press(_keyboard.wKey);
            Release(_keyboard.leftShiftKey);
            _inputs.SprintInput(false);
            Assert.IsTrue(_inputs.sprint);
        }

        [Test]
        public void ShiftとRの同時押しでは反転しない()
        {
            Press(_keyboard.leftShiftKey);
            _inputs.SprintInput(true);
            Press(_keyboard.rKey);
            Release(_keyboard.rKey);
            Release(_keyboard.leftShiftKey);
            _inputs.SprintInput(false);
            Assert.IsTrue(_inputs.sprint);
        }

        [Test]
        public void Shift押下中のクリックでは反転しない()
        {
            Press(_keyboard.leftShiftKey);
            _inputs.SprintInput(true);
            Press(_mouse.leftButton);
            Release(_mouse.leftButton);
            Release(_keyboard.leftShiftKey);
            _inputs.SprintInput(false);
            Assert.IsTrue(_inputs.sprint);
        }

        [Test]
        public void メニュー中はタップで反転しない()
        {
            _inputs.SetSprintToggleAllowed(false);
            _inputs.SprintInput(true);
            _inputs.SprintInput(false);
            Assert.IsTrue(_inputs.sprint);
        }

        [Test]
        public void 入力無効を挟んでも走り状態を維持する()
        {
            _inputs.SprintInput(true);
            _inputs.SprintInput(false);
            Assert.IsFalse(_inputs.sprint);
            _inputs.SetInputEnable(false);
            Assert.IsFalse(_inputs.sprint);
            _inputs.SetInputEnable(true);
            Assert.IsFalse(_inputs.sprint);
        }

        [Test]
        public void ゲームパッドのSprint割当は修飾操作に数えない()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            Press(gamepad.leftTrigger);
            _inputs.SprintInput(true);
            Release(gamepad.leftTrigger);
            _inputs.SprintInput(false);
            Assert.IsFalse(_inputs.sprint);
        }

        [Test]
        public void 走り中に入力無効を挟んでも有効化で復帰する()
        {
            _inputs.SetInputEnable(false);
            Assert.IsFalse(_inputs.sprint);
            _inputs.SetInputEnable(true);
            Assert.IsTrue(_inputs.sprint);
        }

        [Test]
        public void ホールドからトグルへ切り替えると走りで開始する()
        {
            SprintInputModePreference.SetMode(SprintInputMode.Hold);
            Assert.IsFalse(_inputs.sprint);
            SprintInputModePreference.SetMode(SprintInputMode.Toggle);
            Assert.IsTrue(_inputs.sprint);
        }

        [Test]
        public void 設定をホールドへ変えると押下中だけ走る()
        {
            SprintInputModePreference.SetMode(SprintInputMode.Hold);
            Assert.IsFalse(_inputs.sprint);
            _inputs.SprintInput(true);
            Assert.IsTrue(_inputs.sprint);
            _inputs.SprintInput(false);
            Assert.IsFalse(_inputs.sprint);
        }
    }
}
