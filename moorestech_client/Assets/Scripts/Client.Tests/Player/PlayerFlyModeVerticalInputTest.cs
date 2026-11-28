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

namespace Client.Tests.Player
{
    /// <summary>
    ///     フライ中の上下入力（Space上昇・C下降）を確認する
    ///     Verifies the vertical input while flying (Space ascends, C descends)
    /// </summary>
    public class PlayerFlyModeVerticalInputTest : InputTestFixture
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

            // 初期化済みの実TPCを用意
            // Prepare an initialized real TPC
            _playerRoot = new GameObject("PlayerFlyModeVerticalInputTestPlayer");
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
        public void フライ中のSpace押下で上昇しC押下で下降する()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);
            TapSpace(0.0f, 0.1f, 0.2f, 0.3f);

            // Spaceを押している間は上昇入力が届く
            // Holding Space delivers an ascending input
            Press(_keyboard.spaceKey);
            _flyMode.ManualUpdate(1.0f);
            Assert.AreEqual(15f, GetFlightMotion().ResolveVerticalVelocity(0f, 15f, false));

            // Space→Cで下降入力に変わる
            // Switching from Space to C turns it into a descending input
            Release(_keyboard.spaceKey);
            Press(_keyboard.cKey);
            _flyMode.ManualUpdate(2.0f);
            Assert.AreEqual(-15f, GetFlightMotion().ResolveVerticalVelocity(0f, 15f, false));
        }

        [Test]
        public void フライ中のQとEは上下入力にならない()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);
            TapSpace(0.0f, 0.1f, 0.2f, 0.3f);

            Press(_keyboard.eKey);
            _flyMode.ManualUpdate(1.0f);
            Assert.AreEqual(0f, GetFlightMotion().ResolveVerticalVelocity(0f, 15f, false));

            Release(_keyboard.eKey);
            Press(_keyboard.qKey);
            _flyMode.ManualUpdate(2.0f);
            Assert.AreEqual(0f, GetFlightMotion().ResolveVerticalVelocity(0f, 15f, false));
        }

        [Test]
        public void 操作不可へ移ると上下入力を捨てる()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);
            TapSpace(0.0f, 0.1f, 0.2f, 0.3f);
            Press(_keyboard.cKey);
            _flyMode.ManualUpdate(1.0f);

            // ロック中にCを離しても届かないため、移る瞬間に0へ戻す
            // Releasing C during the lock never arrives, so it is zeroed the moment control is lost
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
