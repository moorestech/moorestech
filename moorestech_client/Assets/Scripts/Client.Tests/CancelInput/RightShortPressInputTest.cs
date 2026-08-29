using Client.Game.InGame.UI.UIState.State.CancelInput;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.CancelInput
{
    /// <summary>
    ///     右短押し/右ドラッグ/パネル上押下の判別とReset契約の回帰試験
    ///     Regression tests for short-press vs drag vs press-over-UI classification and the Reset contract
    /// </summary>
    public class RightShortPressInputTest
    {
        private static readonly Vector2 Origin = new(100f, 100f);

        [Test]
        public void パネル外で動かさず離すと短押しが1回だけ成立する()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, Origin, false);
            input.ManualUpdate(true, Origin + new Vector2(2f, 1f), false);
            input.ManualUpdate(false, Origin + new Vector2(2f, 1f), false);

            Assert.IsTrue(input.TryConsumeShortPress());
            Assert.IsFalse(input.TryConsumeShortPress(), "短押しは1度だけ消費される");
        }

        [Test]
        public void 閾値以上動かしてから離すとドラッグ扱いで成立しない()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, Origin, false);
            input.ManualUpdate(true, Origin + new Vector2(RightShortPressInput.MoveThresholdPixels + 1f, 0f), false);
            // 戻ってきても一度ドラッグになった押下は短押しに復帰しない
            // Once a press became a drag it never turns back into a short press, even if the pointer returns
            input.ManualUpdate(true, Origin, false);
            input.ManualUpdate(false, Origin, false);

            Assert.IsFalse(input.TryConsumeShortPress());
        }

        [Test]
        public void パネル上で押した押下は外へ出て離しても成立しない()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, Origin, true);
            input.ManualUpdate(true, Origin, false);
            input.ManualUpdate(false, Origin, false);

            Assert.IsFalse(input.TryConsumeShortPress());
        }

        [Test]
        public void Reset時に押下中だった押下は離されても成立せず次の押下から再武装する()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, Origin, false);
            input.Reset();
            input.ManualUpdate(true, Origin, false);
            input.ManualUpdate(false, Origin, false);
            Assert.IsFalse(input.TryConsumeShortPress(), "Reset前からの押下は捨てる");

            input.ManualUpdate(true, Origin, false);
            input.ManualUpdate(false, Origin, false);
            Assert.IsTrue(input.TryConsumeShortPress(), "離してからの新しい押下は成立する");
        }

        [Test]
        public void Resetは未消費の短押しも捨てる()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, Origin, false);
            input.ManualUpdate(false, Origin, false);
            input.Reset();

            Assert.IsFalse(input.TryConsumeShortPress());
        }

        [Test]
        public void 押下中にResetを二度呼んでも押下は蘇らない()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, Origin, false);
            input.Reset();
            input.Reset();
            input.ManualUpdate(true, Origin, false);
            input.ManualUpdate(false, Origin, false);

            Assert.IsFalse(input.TryConsumeShortPress());
        }

        [Test]
        public void 閾値ちょうど8pxでドラッグになる()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, Origin, false);
            input.ManualUpdate(true, Origin + new Vector2(RightShortPressInput.MoveThresholdPixels, 0f), false);
            input.ManualUpdate(false, Origin + new Vector2(RightShortPressInput.MoveThresholdPixels, 0f), false);

            Assert.IsFalse(input.TryConsumeShortPress(), "ちょうど閾値でドラッグになるため短押しは不成立");
        }

        [Test]
        public void UI外で押してUI上で離すと短押しが成立する()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, Origin, false);
            input.ManualUpdate(false, Origin, true);

            Assert.IsTrue(input.TryConsumeShortPress());
        }
    }
}
