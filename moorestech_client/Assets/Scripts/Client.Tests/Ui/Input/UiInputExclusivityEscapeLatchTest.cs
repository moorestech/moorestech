using Client.Input;
using Client.Tests.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Ui.Input
{
    // ESCを実際に押した状態で、ラッチ前は押下あり・ラッチ後は押下なし・ESC以外とキー解放は影響なしを確かめる
    // With ESC really pressed: press visible before the latch, hidden after; other keys and releases unaffected
    public class UiInputExclusivityEscapeLatchTest : InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();
            TestReflection.ResetInputManagerCache();
        }

        public override void TearDown()
        {
            UiInputExclusivity.ClearEscapeLatchForTest();
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void HybridInputHidesHeldEscapeOnlyAfterLatch()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.escapeKey);
            Press(keyboard.eKey);
            InputSystem.Update();

            Assert.IsTrue(HybridInput.GetKey(KeyCode.Escape));
            UiInputExclusivity.ConsumeEscapeThisFrame();
            Assert.IsFalse(HybridInput.GetKey(KeyCode.Escape));
            Assert.IsFalse(HybridInput.GetKeyDown(KeyCode.Escape));

            // ESC以外は影響を受けない
            // Keys other than ESC are unaffected
            Assert.IsTrue(HybridInput.GetKey(KeyCode.E));
        }

        [Test]
        public void InputKeyBoundToEscapeHidesPressOnlyAfterLatch()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var openMenu = InputManager.UI.OpenMenu;
            var interact = InputManager.Playable.Interact;
            var keyDownCount = 0;
            openMenu.OnGetKeyDown += () => keyDownCount++;
            InputSystem.Update();
            Press(keyboard.escapeKey);
            Press(keyboard.fKey);
            InputSystem.Update();
            openMenu.SetKeyDownForTest(true);
            interact.SetKeyDownForTest(true);

            Assert.AreEqual(1, keyDownCount);
            Assert.IsTrue(openMenu.GetKey);
            Assert.IsTrue(openMenu.GetKeyDown);
            UiInputExclusivity.ConsumeEscapeThisFrame();
            Assert.IsFalse(openMenu.GetKey);
            Assert.IsFalse(openMenu.GetKeyDown);

            // ESCに割り当てていないキーはラッチの影響を受けない
            // A key not bound to ESC is unaffected by the latch
            Assert.IsTrue(interact.GetKey);
            Assert.IsTrue(interact.GetKeyDown);
        }

        [Test]
        public void LatchedEscapeStillDeliversReleaseAndBlocksNewPressEvents()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var openMenu = InputManager.UI.OpenMenu;
            var keyDownCount = 0;
            var keyUpCount = 0;
            openMenu.OnGetKeyDown += () => keyDownCount++;
            openMenu.OnGetKeyUp += () => keyUpCount++;
            InputSystem.Update();

            UiInputExclusivity.ConsumeEscapeThisFrame();
            Press(keyboard.escapeKey);
            InputSystem.Update();
            Assert.AreEqual(0, keyDownCount);

            Release(keyboard.escapeKey);
            InputSystem.Update();
            Assert.AreEqual(1, keyUpCount);
        }
    }
}
