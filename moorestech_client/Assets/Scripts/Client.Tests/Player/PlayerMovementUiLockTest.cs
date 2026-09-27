using System.Reflection;
using Client.Game.InGame.Player;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Player
{
    /// <summary>
    ///     メニュー画面による移動停止が乗車の操作不可と独立に効き、押下状態を正しく揃えることを確かめる
    ///     Verifies the menu movement lock composes with ride controllability and keeps held input consistent
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

            // 本番Prefabと同じくPlayerInputへ入力定義を持たせ、移動系アクションを有効にする
            // Give PlayerInput the same action asset as the production prefab and enable it
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
            _controller.SetControllable(false);
            _controller.SetMovementLockedByUi(true);

            _controller.SetMovementLockedByUi(false);

            Assert.IsFalse(_inputs.inputEnable, "メニューを閉じただけで乗車中の操作不可が解除された");

            _controller.SetControllable(true);
            Assert.IsTrue(_inputs.inputEnable);
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
    }
}
