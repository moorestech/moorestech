using Client.Input;
using Client.Game.InGame.UI.ProgressBar;
using Client.Game.InGame.UI.UIState.State.DragDelete.Confirm;
using Client.Tests.Common;
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
            TestReflection.ResetInputManagerCache();
        }

        public override void TearDown()
        {
            WebUiInputExclusivity.SetState(false, false);
            TestReflection.ResetInputManagerCache();
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

        [TestCase(false)]
        [TestCase(true)]
        public void テキスト入力中の解放で長押し待機を解除する(bool cancelled)
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var interact = InputManager.Playable.Interact;
            var hold = new DeleteHoldConfirm(new ProgressBarState());
            interact.OnGetKeyUp += () => hold.Advance(false, 0f);
            InputSystem.Update();
            Press(keyboard.fKey);
            InputSystem.Update();
            hold.SetSubject(DeleteHoldSubject.Selection(1));
            if (cancelled) hold.Cancel(true);
            else Assert.IsTrue(hold.Advance(true, 2f));

            // 実解放のないモード再入場では、満了・取消の待機を維持する
            // Re-entering without a physical release preserves completion and cancellation latches
            hold.Reset();
            hold.SetSubject(DeleteHoldSubject.Selection(2));
            Assert.IsFalse(hold.Advance(true, 3f));
            WebUiInputExclusivity.SetState(false, true);
            Release(keyboard.fKey);
            InputSystem.Update();
            var suppressedPresses = 0;
            interact.OnGetKeyDown += () => suppressedPresses++;
            Press(keyboard.fKey);
            InputSystem.Update();
            Assert.IsFalse(interact.GetKey);
            Assert.AreEqual(0, suppressedPresses);

            // 検索欄から戻ったときは、保持中のキーで次の長押しを開始できる
            // After leaving text input, the held key can start the next confirmation
            WebUiInputExclusivity.SetState(false, false);
            Assert.IsTrue(interact.GetKey);
            hold.SetSubject(DeleteHoldSubject.Selection(2));
            Assert.IsTrue(hold.Advance(interact.GetKey, 2f));
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
