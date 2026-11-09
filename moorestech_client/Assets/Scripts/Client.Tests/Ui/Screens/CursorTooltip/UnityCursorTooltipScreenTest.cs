using System.Collections.Generic;
using Client.Input;
using Client.Input.Pointer;
using Client.Ui.Foundation.Input.Pointer;
using Client.Ui.Screens.CursorTooltip;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// 本番の実画面: ロック切替を書き換えた後に知らせ、ポインタは実画面 px のまま渡す
    /// Production screen: notifies lock toggles after writing them, and hands pointers over in screen px
    /// </summary>
    public class UnityCursorTooltipScreenTest : InputTestFixture
    {
        [TearDown]
        public void UnlockCursor()
        {
            InputManager.MouseCursorVisible(true);
        }

        [Test]
        public void ロックの切替をロック状態を書き換えた後に一度ずつ知らせる()
        {
            InputSystem.AddDevice<Mouse>();
            InputManager.MouseCursorVisible(true);
            var screen = new UnityCursorTooltipScreen();
            var lockStatesSeenOnNotify = new List<CursorLockMode>();
            screen.OnAnchorModeChanged.Subscribe(_ => lockStatesSeenOnNotify.Add(UnityEngine.Cursor.lockState));

            // 同じ状態の再指定は知らせない
            // Re-applying the same state does not notify
            InputManager.MouseCursorVisible(false);
            InputManager.MouseCursorVisible(false);
            InputManager.MouseCursorVisible(true);
            InputManager.MouseCursorVisible(true);

            Assert.AreEqual(new[] { CursorLockMode.Locked, CursorLockMode.None }, lockStatesSeenOnNotify.ToArray());
        }

        [Test]
        public void ポインタは実画面pxのまま流し今の位置も実画面pxで返す()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var pointer = new InputSystemUiScreenPointer(new InputSystemPointerPositionStream());
            var received = new List<Vector2>();
            pointer.OnPointerMovedOnScreen.Subscribe(received.Add);

            Set(mouse.position, new Vector2(120f, 80f));

            Assert.AreEqual(new[] { new Vector2(120f, 80f) }, received.ToArray());
            Assert.AreEqual(new Vector2(120f, 80f), pointer.CurrentPointerOnScreen());
        }
    }
}
