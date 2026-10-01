using System.Reflection;
using Client.Game;
using Client.Game.InGame.Player;
using Client.Game.InGame.Player.FlyMode;
using Client.Input;
using Client.Tests.Common;
using Common.Debug;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Player
{
    /// <summary>
    ///     スペース連打によるフライモードの入退を確認する
    ///     Verifies entering and leaving fly mode by tapping space
    /// </summary>
    public class PlayerFlyModeControllerTest : InputTestFixture
    {
        private GameObject _playerRoot;
        private PlayerFlyModeController _flyMode;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            TestReflection.ResetInputManagerCache();
            _ = InputManager.Player;
            InputSystem.Update();

            // 実際のThirdPersonControllerを初期化済みで用意する
            // Prepare a real, initialized ThirdPersonController
            _playerRoot = new GameObject("PlayerFlyModeControllerTestPlayer");
            _playerRoot.AddComponent<CharacterController>();
            _playerRoot.AddComponent<StarterAssetsInputs>();
            var thirdPersonController = _playerRoot.AddComponent<ThirdPersonController>();
            var playerObjectController = _playerRoot.AddComponent<PlayerObjectController>();
            typeof(PlayerObjectController).GetField("controller", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(playerObjectController, thirdPersonController);
            playerObjectController.Initialize(Vector3.zero, Vector3.zero);
            _flyMode = new PlayerFlyModeController(thirdPersonController);
        }

        public override void TearDown()
        {
            InputManager.Player.Jump.SetKeyDownForTest(false);
            Object.DestroyImmediate(_playerRoot);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void トグルオンで4連打するとフライに入る()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);

            TapSpace(true, 0.0f, 0.1f, 0.2f, 0.3f);

            Assert.IsTrue(_flyMode.IsFlying);
        }

        [Test]
        public void 押下間隔が03秒を超えると数え直す()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);

            TapSpace(true, 0.0f, 0.1f, 0.5f, 0.6f);

            Assert.IsFalse(_flyMode.IsFlying);

            // 数え直した2回に続けて2回押せば成立する
            // Two more taps after the restarted pair complete the sequence
            TapSpace(true, 0.7f, 0.8f);
            Assert.IsTrue(_flyMode.IsFlying);
        }

        [Test]
        public void トグルオフでは4連打してもフライに入らない()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, false);

            TapSpace(true, 0.0f, 0.1f, 0.2f, 0.3f);

            Assert.IsFalse(_flyMode.IsFlying);
        }

        [Test]
        public void フライ中に2連打すると解除する()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);
            TapSpace(true, 0.0f, 0.1f, 0.2f, 0.3f);

            TapSpace(true, 1.0f, 1.1f);

            Assert.IsFalse(_flyMode.IsFlying);
        }

        [Test]
        public void フライ中の間隔の空いた単押しでは解除しない()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);
            TapSpace(true, 0.0f, 0.1f, 0.2f, 0.3f);

            TapSpace(true, 1.0f, 1.5f);

            Assert.IsTrue(_flyMode.IsFlying);
        }

        [Test]
        public void トグルをオフにしてもフライ中は2連打で解除できる()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);
            TapSpace(true, 0.0f, 0.1f, 0.2f, 0.3f);
            DebugParameters.SaveBool(DebugConst.FlyModeKey, false);

            Assert.IsTrue(_flyMode.IsFlying, "トグルオフだけでは解除しない");
            TapSpace(true, 1.0f, 1.1f);
            Assert.IsFalse(_flyMode.IsFlying);
        }

        [Test]
        public void 操作不可の間の押下は数えず途中の連打も捨てる()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);

            TapSpace(true, 0.0f, 0.1f);
            TapSpace(false, 0.15f, 0.2f);
            TapSpace(true, 0.25f, 0.3f);

            Assert.IsFalse(_flyMode.IsFlying, "ロックを挟んだ連打が通算された");
        }

        private void TapSpace(bool isControllable, params float[] tapTimes)
        {
            // 押下フレームと非押下フレームを交互に流す
            // Alternate pressed and released frames
            foreach (var tapTime in tapTimes)
            {
                InputManager.Player.Jump.SetKeyDownForTest(true);
                _flyMode.ManualUpdate(isControllable, tapTime);
                InputManager.Player.Jump.SetKeyDownForTest(false);
                _flyMode.ManualUpdate(isControllable, tapTime + 0.01f);
            }
        }
    }
}
