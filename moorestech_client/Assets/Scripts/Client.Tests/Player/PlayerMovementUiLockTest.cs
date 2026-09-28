using System.Reflection;
using Client.Game.InGame.Player;
using Client.Input;
using Client.Tests.Common;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Player
{
    /// <summary>
    ///     メニュー移動停止と乗車操作不可の独立性を確認
    ///     Verifies menu lock is independent of ride lock
    /// </summary>
    public class PlayerMovementUiLockTest : InputTestFixture
    {
        private GameObject _playerRoot;
        private Keyboard _keyboard;
        private StarterAssetsInputs _inputs;
        private PlayerObjectController _controller;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();

            // 押下の読み直しはInputManager経由なので、他テストが張った静的キャッシュを捨てて張り直す
            // The held-key reread goes through InputManager, so drop the static cache an earlier test left behind
            TestReflection.ResetInputManagerCache();

            // 押下の実値を読めるよう、デバイス登録後に入力アセットを生成・有効化しておく
            // Build and enable the input asset after the device exists so held keys can be read back
            _ = InputManager.Player;
            InputSystem.Update();

            _playerRoot = new GameObject("PlayerMovementUiLockTestPlayer");
            _playerRoot.AddComponent<CharacterController>();
            _inputs = _playerRoot.AddComponent<StarterAssetsInputs>();
            var thirdPersonController = _playerRoot.AddComponent<ThirdPersonController>();

            _controller = _playerRoot.AddComponent<PlayerObjectController>();
            typeof(PlayerObjectController).GetField("controller", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_controller, thirdPersonController);
            _controller.Initialize(Vector3.zero, Vector3.zero);
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(_playerRoot);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void メニューで止めると押しっぱなしの移動値が残らない()
        {
            _inputs.MoveInput(new Vector2(0f, 1f));
            _inputs.SprintInput(true);
            _inputs.JumpInput(true);

            _controller.SetMovementLock(PlayerMovementLockReason.Ui, true);

            // 無効中は離しが届かないため、残すとメニューの間ずっと歩き続ける
            // Releases never arrive while disabled, so a leftover value would keep walking through the menu
            Assert.IsFalse(_inputs.inputEnable);
            Assert.AreEqual(Vector2.zero, _inputs.move);
            Assert.IsFalse(_inputs.sprint);
            Assert.IsFalse(_inputs.jump, "押しっぱなしのジャンプが残るとメニュー越しに跳ね続ける");
        }

        [Test]
        public void 乗車中はメニューを閉じても操作不可のまま()
        {
            var trainCar = new GameObject("PlayerMovementUiLockTestTrainCar");
            _controller.SetRideFollowTarget(trainCar.transform, Vector3.zero, Quaternion.identity);
            _controller.SetMovementLock(PlayerMovementLockReason.Ui, true);

            _controller.SetMovementLock(PlayerMovementLockReason.Ui, false);

            Assert.IsFalse(_inputs.inputEnable, "メニューを閉じただけで乗車中の操作不可が解除された");

            _controller.ClearRideFollowTarget();
            Assert.IsTrue(_inputs.inputEnable);
            Object.DestroyImmediate(trainCar);
        }

        [Test]
        public void デバッグ停止中はメニューを閉じても操作不可のまま()
        {
            _controller.SetMovementLock(PlayerMovementLockReason.Debug, true);
            _controller.SetMovementLock(PlayerMovementLockReason.Ui, true);

            _controller.SetMovementLock(PlayerMovementLockReason.Ui, false);

            Assert.IsFalse(_inputs.inputEnable, "メニューを閉じただけでデバッグ停止が解除された");

            _controller.SetMovementLock(PlayerMovementLockReason.Debug, false);
            Assert.IsTrue(_inputs.inputEnable);
        }

        [Test]
        public void 非メニュー画面どうしの遷移ではジャンプ入力を捨てない()
        {
            _inputs.JumpInput(true);

            // 非メニュー間の遷移でも解除が再適用されるため、同値なら何もしないこと
            // Non-menu transitions reapply the unlock too, so a same-value call must be a no-op
            _controller.SetMovementLock(PlayerMovementLockReason.Ui, false);

            Assert.IsTrue(_inputs.jump);
        }

        [Test]
        public void メニューを閉じた時点で押しているキーから移動を再開する()
        {
            _controller.SetMovementLock(PlayerMovementLockReason.Ui, true);
            Press(_keyboard.wKey);
            InputSystem.Update();

            _controller.SetMovementLock(PlayerMovementLockReason.Ui, false);

            // 押下イベントはメニュー中に捨てられているので、解除時に実値を読み直していなければ止まったままになる
            // The press was dropped during the menu, so without rereading the live value the player would stay still
            Assert.IsTrue(_inputs.inputEnable);
            Assert.AreEqual(new Vector2(0f, 1f), _inputs.move);
        }

        [Test]
        public void メニューを閉じた時点でダッシュキーを押していればダッシュを再開する()
        {
            _controller.SetMovementLock(PlayerMovementLockReason.Ui, true);
            Press(_keyboard.wKey);
            Press(_keyboard.leftShiftKey);
            InputSystem.Update();

            _controller.SetMovementLock(PlayerMovementLockReason.Ui, false);

            Assert.IsTrue(_inputs.sprint);
        }
    }
}
