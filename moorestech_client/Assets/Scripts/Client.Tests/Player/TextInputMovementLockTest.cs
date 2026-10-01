using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Player;
using Client.Input;
using Client.Tests.Common;
using NUnit.Framework;
using StarterAssets;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Player
{
    /// <summary>
    ///     Web UIの文字入力欄にフォーカスがある間は打鍵が移動・ジャンプ・ダッシュに漏れないことを確認
    ///     Verifies keystrokes never leak into walk, jump or sprint while a Web UI text field owns focus
    /// </summary>
    public class TextInputMovementLockTest : InputTestFixture
    {
        private GameObject _playerRoot;
        private Keyboard _keyboard;
        private StarterAssetsInputs _inputs;
        private PlayerObjectController _controller;
        private Subject<bool> _textInputFocusedChanged;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();

            // 押下の読み直しはInputManager経由なので、静的キャッシュを捨ててデバイス登録後に張り直す
            // The held-key reread goes through InputManager, so drop its static cache and rebuild after adding the device
            TestReflection.ResetInputManagerCache();
            _ = InputManager.Player;
            InputSystem.Update();

            _playerRoot = new GameObject("TextInputMovementLockTestPlayer");
            _playerRoot.AddComponent<CharacterController>();
            _inputs = _playerRoot.AddComponent<StarterAssetsInputs>();
            var thirdPersonController = _playerRoot.AddComponent<ThirdPersonController>();
            _controller = _playerRoot.AddComponent<PlayerObjectController>();
            typeof(PlayerObjectController).GetField("controller", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_controller, thirdPersonController);
            _controller.Initialize(Vector3.zero, Vector3.zero);

            _textInputFocusedChanged = new Subject<bool>();
            new TextInputMovementLockApplier(_controller).Initialize(_textInputFocusedChanged, false);
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(_playerRoot);
            WebUiInputExclusivity.SetState(false, false);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void 文字入力中は押しっぱなしの移動もジャンプもダッシュも残らない()
        {
            _inputs.MoveInput(new Vector2(0f, 1f));
            _inputs.SprintInput(true);
            _inputs.JumpInput(true);

            _textInputFocusedChanged.OnNext(true);

            // 検索欄で打ったw・空白・大文字のShiftが歩行・ジャンプ・ダッシュにならない
            // A typed w, space or capitalizing Shift in the search box must not walk, jump or sprint
            Assert.IsFalse(_inputs.inputEnable);
            Assert.AreEqual(Vector2.zero, _inputs.move);
            Assert.IsFalse(_inputs.jump);
            Assert.IsFalse(_inputs.sprint);
        }

        [Test]
        public void 文字入力を抜けた時点で押しているキーから移動を再開する()
        {
            _textInputFocusedChanged.OnNext(true);
            Press(_keyboard.wKey);
            Press(_keyboard.leftShiftKey);
            InputSystem.Update();

            _textInputFocusedChanged.OnNext(false);

            Assert.IsTrue(_inputs.inputEnable);
            Assert.AreEqual(new Vector2(0f, 1f), _inputs.move);
            Assert.IsTrue(_inputs.sprint);
        }

        [Test]
        public void 文字入力を抜けてもメニューの停止は残る()
        {
            _controller.SetMovementLock(PlayerMovementLockReason.Ui, true);
            _textInputFocusedChanged.OnNext(true);

            _textInputFocusedChanged.OnNext(false);

            Assert.IsFalse(_inputs.inputEnable, "文字入力の解除がポーズメニューの停止まで外した");
        }

        [Test]
        public void 購読前から文字入力中なら初期化の時点で止まる()
        {
            var playerRoot = new GameObject("TextInputMovementLockTestLatePlayer");
            playerRoot.AddComponent<CharacterController>();
            var inputs = playerRoot.AddComponent<StarterAssetsInputs>();
            var thirdPersonController = playerRoot.AddComponent<ThirdPersonController>();
            var controller = playerRoot.AddComponent<PlayerObjectController>();
            typeof(PlayerObjectController).GetField("controller", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, thirdPersonController);
            controller.Initialize(Vector3.zero, Vector3.zero);

            new TextInputMovementLockApplier(controller).Initialize(new Subject<bool>(), true);

            Assert.IsFalse(inputs.inputEnable);
            Object.DestroyImmediate(playerRoot);
        }

        [Test]
        public void フォーカス状態は値が変わった時だけ通知される()
        {
            WebUiInputExclusivity.SetState(false, false);
            var received = new List<bool>();
            var subscription = WebUiInputExclusivity.OnTextInputFocusedChanged.Subscribe(received.Add);

            // ポインタだけの変化や同値の再送では通知せず、切断時リセットの解除は通知する
            // Pointer-only changes and same-value resends stay silent; the disconnect reset's release is notified
            WebUiInputExclusivity.SetState(true, false);
            WebUiInputExclusivity.SetState(true, true);
            WebUiInputExclusivity.SetState(false, true);
            WebUiInputExclusivity.SetState(false, false);
            subscription.Dispose();

            CollectionAssert.AreEqual(new[] { true, false }, received);
        }
    }
}
