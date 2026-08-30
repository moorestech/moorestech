using Client.Input;
using Client.Tests.Mining;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Client.Tests.Input
{
    public class PlayableInteractInputTest : InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();

            // 他テストが張ったInputManagerの静的キャッシュは破棄済みデバイスを参照したままなので張り直す
            // Drop InputManager's static cache so it doesn't keep referencing a device disposed by an earlier test
            MiningTestReflection.ResetInputManagerCache();
        }

        public override void TearDown()
        {
            MiningTestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void FキーがInteractとして読める()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var interact = InputManager.Playable.Interact;
            // WasPressedThisFrame()はEditModeでは(InputUpdateType.Editorのため)発火しないので購読で検知する
            // WasPressedThisFrame() never fires in EditMode (it runs under InputUpdateType.Editor), so detect via subscription instead
            var keyDownFired = false;
            interact.OnGetKeyDown += () => keyDownFired = true;
            InputSystem.Update();
            Press(keyboard.fKey);
            InputSystem.Update();
            Assert.IsTrue(interact.GetKey);
            Assert.IsTrue(keyDownFired);
        }

        [Test]
        public void EキーがRideとして読める()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var ride = InputManager.Playable.Ride;
            var keyDownFired = false;
            ride.OnGetKeyDown += () => keyDownFired = true;
            InputSystem.Update();
            Press(keyboard.eKey);
            InputSystem.Update();
            Assert.IsTrue(keyDownFired);
        }
    }
}
