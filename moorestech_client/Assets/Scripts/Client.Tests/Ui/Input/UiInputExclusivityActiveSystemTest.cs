using System.Collections.Generic;
using Client.Game.InGame.Control;
using Client.Input;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.Ui.Input
{
    // 選ばれた系だけを読むことを固定する
    // Pins that only the chosen system is read
    public class UiInputExclusivityActiveSystemTest
    {
        private class AlwaysOverProbe : IGameUiPointerProbe
        {
            public bool IsPointerOverGameUi(Vector2 screenPosition) => true;
        }

        [TearDown]
        public void TearDown()
        {
            UiInputExclusivity.SetState(false, false);
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.SetGameUiPointerProbe(null);
            UiInputExclusivity.SetActiveUiSystem(UiSystem.Web);
            InputManager.MouseCursorVisible(true);
        }

        [Test]
        public void ロック中はUIの上に乗っていても乗っていないと答える()
        {
            // 判定はロック状態の正本を読む。batchmode でも Cursor.lockState を読まないので CI で効く
            // The check reads the lock-state source of truth, so it holds in batchmode CI where Cursor.lockState stays None
            UiInputExclusivity.SetActiveUiSystem(UiSystem.Web);
            UiInputExclusivity.SetState(true, false);
            InputManager.MouseCursorVisible(false);
            Assert.IsFalse(UiPointerHitTest.IsPointerOverAnyUi());

            InputManager.MouseCursorVisible(true);
            Assert.IsTrue(UiPointerHitTest.IsPointerOverAnyUi());
        }

        [Test]
        public void Web系ではWebの値だけを読みGameUiの値を読まない()
        {
            UiInputExclusivity.SetActiveUiSystem(UiSystem.Web);
            UiInputExclusivity.SetGameUiPointerProbe(new AlwaysOverProbe());
            UiInputExclusivity.SetGameUiTextInputFocused(true);

            // 他方の占有は無視される
            // The other side occupancy is ignored
            Assert.IsFalse(UiInputExclusivity.IsPointerOverGameUi(Vector2.zero));
            Assert.IsFalse(UiInputExclusivity.IsPointerOverActiveUi(Vector2.zero));
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);

            UiInputExclusivity.SetState(true, true);
            Assert.IsTrue(UiInputExclusivity.IsPointerOverActiveUi(Vector2.zero));
            // 公開の判定も Web の値を読む
            // The public hit test also reads the Web value
            Assert.IsTrue(UiPointerHitTest.IsPointerOverAnyUi());
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
            Assert.IsTrue(UiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));
        }

        [Test]
        public void GameUi系ではGameUiの値だけを読みWebの値を読まない()
        {
            UiInputExclusivity.SetActiveUiSystem(UiSystem.GameUi);
            UiInputExclusivity.SetState(true, true);

            // 他方の占有は無視される
            // The other side occupancy is ignored
            Assert.IsFalse(UiInputExclusivity.IsPointerOverWebUi);
            Assert.IsFalse(UiInputExclusivity.IsPointerOverActiveUi(Vector2.zero));
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);

            UiInputExclusivity.SetGameUiPointerProbe(new AlwaysOverProbe());
            UiInputExclusivity.SetGameUiTextInputFocused(true);
            Assert.IsTrue(UiInputExclusivity.IsPointerOverActiveUi(Vector2.zero));
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void 系の切替で文字入力の占有を計算し直し購読者へ届ける()
        {
            UiInputExclusivity.SetActiveUiSystem(UiSystem.Web);
            UiInputExclusivity.SetState(false, true);
            var received = new List<bool>();
            using var subscription = UiInputExclusivity.TextInputFocused.Subscribe(received.Add);

            // Web の true → GameUi へ切替で false → GameUi の欄で true。Web へ戻しても Web の値も true なので届かない
            // Web true -> false on switching to GameUi -> true from a GameUi field; switching back to Web stays true so nothing new arrives
            UiInputExclusivity.SetActiveUiSystem(UiSystem.GameUi);
            UiInputExclusivity.SetGameUiTextInputFocused(true);
            UiInputExclusivity.SetActiveUiSystem(UiSystem.Web);

            CollectionAssert.AreEqual(new[] { true, false, true }, received);
        }
    }
}
