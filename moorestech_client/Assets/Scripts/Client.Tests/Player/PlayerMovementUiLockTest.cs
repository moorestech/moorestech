using System.Reflection;
using Client.Game.InGame.Player;
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
        private MoorestechInputSettings _inputSettings;
        private StarterAssetsInputs _inputs;
        private PlayerObjectController _controller;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();

            // 本番同様に入力定義を付与し有効化
            // Match production: attach and enable actions
            _playerRoot = new GameObject("PlayerMovementUiLockTestPlayer");
            _playerRoot.AddComponent<CharacterController>();
            _inputs = _playerRoot.AddComponent<StarterAssetsInputs>();
            var thirdPersonController = _playerRoot.AddComponent<ThirdPersonController>();
            _inputSettings = new MoorestechInputSettings();
            _playerRoot.GetComponent<PlayerInput>().actions = _inputSettings.asset;
            _inputSettings.asset.Enable();

            _controller = _playerRoot.AddComponent<PlayerObjectController>();
            typeof(PlayerObjectController).GetField("controller", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_controller, thirdPersonController);
            _controller.Initialize(Vector3.zero, Vector3.zero);
        }

        public override void TearDown()
        {
            _inputSettings.asset.Disable();
            Object.DestroyImmediate(_playerRoot);
            base.TearDown();
        }

        [Test]
        public void メニューで止めると押しっぱなしの移動値が残らない()
        {
            _inputs.MoveInput(new Vector2(0f, 1f));
            _inputs.SprintInput(true);

            _controller.SetMovementLockedByUi(true);

            // 無効中は離しが届かないため、残すとメニューの間ずっと歩き続ける
            // Releases never arrive while disabled, so a leftover value would keep walking through the menu
            Assert.IsFalse(_inputs.inputEnable);
            Assert.AreEqual(Vector2.zero, _inputs.move);
            Assert.IsFalse(_inputs.sprint);
        }

        [Test]
        public void 乗車中はメニューを閉じても操作不可のまま()
        {
            var trainCar = new GameObject("PlayerMovementUiLockTestTrainCar");
            _controller.SetRideFollowTarget(trainCar.transform, Vector3.zero, Quaternion.identity);
            _controller.SetMovementLockedByUi(true);

            _controller.SetMovementLockedByUi(false);

            Assert.IsFalse(_inputs.inputEnable, "メニューを閉じただけで乗車中の操作不可が解除された");

            _controller.ClearRideFollowTarget();
            Assert.IsTrue(_inputs.inputEnable);
            Object.DestroyImmediate(trainCar);
        }

        [Test]
        public void デバッグ停止中はメニューを閉じても操作不可のまま()
        {
            _controller.SetMovementLockedByDebug(true);
            _controller.SetMovementLockedByUi(true);

            _controller.SetMovementLockedByUi(false);

            Assert.IsFalse(_inputs.inputEnable, "メニューを閉じただけでデバッグ停止が解除された");

            _controller.SetMovementLockedByDebug(false);
            Assert.IsTrue(_inputs.inputEnable);
        }

        [Test]
        public void 非メニュー画面どうしの遷移ではジャンプ入力を捨てない()
        {
            _inputs.JumpInput(true);

            // 非メニュー間の遷移でも解除が再適用されるため、同値なら何もしないこと
            // Non-menu transitions reapply the unlock too, so a same-value call must be a no-op
            _controller.SetMovementLockedByUi(false);

            Assert.IsTrue(_inputs.jump);
        }

        [Test]
        public void メニューを閉じた時点で押しているキーから移動を再開する()
        {
            _controller.SetMovementLockedByUi(true);
            Press(_keyboard.wKey);

            _controller.SetMovementLockedByUi(false);

            // 押下イベントはメニュー中に捨てられているので、解除時に実値を読み直していなければ止まったままになる
            // The press was dropped during the menu, so without rereading the live value the player would stay still
            Assert.IsTrue(_inputs.inputEnable);
            Assert.AreEqual(new Vector2(0f, 1f), _inputs.move);
        }

        [Test]
        public void メニューを閉じた時点でダッシュキーを押していればダッシュを再開する()
        {
            _controller.SetMovementLockedByUi(true);
            Press(_keyboard.wKey);
            Press(_keyboard.leftShiftKey);

            _controller.SetMovementLockedByUi(false);

            Assert.IsTrue(_inputs.sprint);
        }
    }
}
