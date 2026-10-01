using System.Reflection;
using Client.Game;
using Client.Game.InGame.Player.FlyMode;
using Client.Input;
using Client.Tests.Common;
using Common.Debug;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Client.Tests.Player
{
    /// <summary>
    ///     スペース連打によるフライモードの入退を確認する
    ///     Verifies entering and leaving fly mode by tapping space
    /// </summary>
    public class PlayerFlyModeControllerTest : InputTestFixture
    {
        private GameObject _playerRoot;
        private ThirdPersonController _thirdPersonController;
        private PlayerFlyModeController _flyMode;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            TestReflection.ResetInputManagerCache();
            _ = InputManager.Player;
            InputSystem.Update();

            // 実際のThirdPersonControllerを初期化済みで用意する
            // Prepare a real, initialized ThirdPersonController
            _playerRoot = new GameObject("PlayerFlyModeControllerTestPlayer");
            _playerRoot.AddComponent<CharacterController>();
            _playerRoot.AddComponent<StarterAssetsInputs>();
            _thirdPersonController = _playerRoot.AddComponent<ThirdPersonController>();
            _thirdPersonController.Initialize();
            _flyMode = new PlayerFlyModeController(_thirdPersonController);
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

            // 3回では入らず、4回目で入る
            // Three taps do not enter; the fourth does
            TapSpace(0.0f, 0.1f, 0.2f);
            Assert.IsFalse(_thirdPersonController.IsFlying());
            TapSpace(0.3f);

            Assert.IsTrue(_thirdPersonController.IsFlying());
        }

        [Test]
        public void フライ中のE押下が上下入力としてThirdPersonControllerへ届く()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);
            TapSpace(0.0f, 0.1f, 0.2f, 0.3f);

            Press(_keyboard.eKey);
            _flyMode.ManualUpdate(1.0f);

            Assert.AreEqual(15f, GetFlightMotion().ResolveVerticalVelocity(0f, 15f, false));
        }

        [Test]
        public void 押下間隔が03秒を超えると数え直す()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);

            TapSpace(0.0f, 0.1f, 0.5f, 0.6f);
            Assert.IsFalse(_thirdPersonController.IsFlying());

            // 数え直した2回に続けて2回押せば成立する
            // Two more taps after the restarted pair complete the sequence
            TapSpace(0.7f, 0.8f);
            Assert.IsTrue(_thirdPersonController.IsFlying());
        }

        [Test]
        public void トグルオフでは4連打してもフライに入らない()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, false);
            TapSpace(0.0f, 0.1f, 0.2f, 0.3f);

            Assert.IsFalse(_thirdPersonController.IsFlying());
        }

        [Test]
        public void フライ中に2連打すると解除する()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);
            TapSpace(0.0f, 0.1f, 0.2f, 0.3f);

            TapSpace(1.0f, 1.1f);

            Assert.IsFalse(_thirdPersonController.IsFlying());
        }

        [Test]
        public void 入った直後の5打目は解除の1打目として数え直す()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);

            // 入った瞬間に連打を数え直さないと、5打目が2連打目扱いになり即解除する
            // Without restarting at entry, the fifth tap would count as the second and leave at once
            TapSpace(0.0f, 0.1f, 0.2f, 0.3f, 0.4f);

            Assert.IsTrue(_thirdPersonController.IsFlying());
        }

        [Test]
        public void フライ中の間隔の空いた単押しでは解除しない()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);
            TapSpace(0.0f, 0.1f, 0.2f, 0.3f);

            TapSpace(1.0f, 1.5f);

            Assert.IsTrue(_thirdPersonController.IsFlying());
        }

        [Test]
        public void トグルをオフにしてもフライ中は2連打で解除できる()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);
            TapSpace(0.0f, 0.1f, 0.2f, 0.3f);
            DebugParameters.SaveBool(DebugConst.FlyModeKey, false);

            Assert.IsTrue(_thirdPersonController.IsFlying(), "トグルオフだけでは解除しない");
            TapSpace(1.0f, 1.1f);
            Assert.IsFalse(_thirdPersonController.IsFlying());
        }

        [Test]
        public void 操作不可の間の押下は数えず途中の連打も捨てる()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);

            TapSpace(0.0f, 0.1f);
            _flyMode.SetControllable(false);
            LogAssert.Expect(LogType.Log, "[FlyMode] Space ignored: player is not controllable (movement lock or riding)");
            LogAssert.Expect(LogType.Log, "[FlyMode] Space ignored: player is not controllable (movement lock or riding)");
            TapSpace(0.15f, 0.2f);
            _flyMode.SetControllable(true);
            TapSpace(0.25f, 0.3f);

            Assert.IsFalse(_thirdPersonController.IsFlying(), "ロックを挟んだ連打が通算された");
        }

        [Test]
        public void 操作不可へ移ると上下入力を捨てる()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);
            TapSpace(0.0f, 0.1f, 0.2f, 0.3f);
            Press(_keyboard.eKey);
            _flyMode.ManualUpdate(1.0f);

            // ロック中にEを離しても届かないため、移る瞬間に0へ戻す
            // Releasing E during the lock never arrives, so it is zeroed the moment control is lost
            _flyMode.SetControllable(false);

            Assert.AreEqual(0f, GetFlightMotion().ResolveVerticalVelocity(0f, 15f, false));
        }

        private PlayerFlightMotion GetFlightMotion()
        {
            // 上下入力の公開読み口は無いため、委譲先を直接見る
            // There is no public reader for vertical input, so inspect the delegate directly
            return (PlayerFlightMotion)typeof(ThirdPersonController).GetField("_flightMotion", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_thirdPersonController);
        }

        private void TapSpace(params float[] tapTimes)
        {
            // 押下フレームと非押下フレームを交互に流す
            // Alternate pressed and released frames
            foreach (var tapTime in tapTimes)
            {
                InputManager.Player.Jump.SetKeyDownForTest(true);
                _flyMode.ManualUpdate(tapTime);
                InputManager.Player.Jump.SetKeyDownForTest(false);
                _flyMode.ManualUpdate(tapTime + 0.01f);
            }
        }
    }
}
