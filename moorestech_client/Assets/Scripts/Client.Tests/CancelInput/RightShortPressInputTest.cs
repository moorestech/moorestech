using Client.Game.InGame.UI.UIState.State.CancelInput;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.CancelInput
{
    /// <summary>
    ///     右短押し/右ドラッグ/パネル上押下の判別とReset契約の回帰試験。入力はフレーム毎のマウス移動量で与える
    ///     Regression tests for short-press vs drag vs press-over-UI classification and the Reset contract; input is given as per-frame mouse deltas
    /// </summary>
    public class RightShortPressInputTest
    {
        private static readonly Vector2 NoMove = Vector2.zero;

        [Test]
        public void パネル外で動かさず離すと短押しが1回だけ成立する()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, NoMove, false);
            input.ManualUpdate(true, new Vector2(2f, 1f), false);
            input.ManualUpdate(false, NoMove, false);

            Assert.IsTrue(input.TryConsumeShortPress());
            Assert.IsFalse(input.TryConsumeShortPress(), "短押しは1度だけ消費される");
        }

        [Test]
        public void 閾値以上動かしてから離すとドラッグ扱いで成立しない()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, NoMove, false);
            input.ManualUpdate(true, new Vector2(9f, 0f), false);
            // 逆向きに戻しても一度ドラッグになった押下は短押しに復帰しない
            // Once a press became a drag it never turns back into a short press, even when the pointer moves back
            input.ManualUpdate(true, new Vector2(-9f, 0f), false);
            input.ManualUpdate(false, NoMove, false);

            Assert.IsFalse(input.TryConsumeShortPress());
        }

        [Test]
        public void 一度に閾値未満でも累積が閾値を超えるとドラッグ扱いになる()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, NoMove, false);
            input.ManualUpdate(true, new Vector2(3f, 0f), false);
            input.ManualUpdate(true, new Vector2(3f, 0f), false);
            input.ManualUpdate(true, new Vector2(3f, 0f), false);
            input.ManualUpdate(false, NoMove, false);

            Assert.IsFalse(input.TryConsumeShortPress(), "3pxを3回で累積9pxとなりドラッグ扱い");
        }

        [Test]
        public void パネル上で押した押下は外へ出て離しても成立しない()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, NoMove, true);
            input.ManualUpdate(true, NoMove, false);
            input.ManualUpdate(false, NoMove, false);

            Assert.IsFalse(input.TryConsumeShortPress());
        }

        [Test]
        public void Reset時に押下中だった押下は離されても成立せず次の押下から再武装する()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, NoMove, false);
            input.Reset(true);
            input.ManualUpdate(true, NoMove, false);
            input.ManualUpdate(false, NoMove, false);
            Assert.IsFalse(input.TryConsumeShortPress(), "Reset前からの押下は捨てる");

            input.ManualUpdate(true, NoMove, false);
            input.ManualUpdate(false, NoMove, false);
            Assert.IsTrue(input.TryConsumeShortPress(), "離してからの新しい押下は成立する");
        }

        [Test]
        public void Resetは未消費の短押しも捨てる()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, NoMove, false);
            input.ManualUpdate(false, NoMove, false);
            input.Reset(false);

            Assert.IsFalse(input.TryConsumeShortPress());
        }

        [Test]
        public void 押下中にResetを二度呼んでも押下は蘇らない()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, NoMove, false);
            input.Reset(true);
            input.Reset(true);
            input.ManualUpdate(true, NoMove, false);
            input.ManualUpdate(false, NoMove, false);

            Assert.IsFalse(input.TryConsumeShortPress());
        }

        [Test]
        public void 累積ちょうど8pxでドラッグになる()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, NoMove, false);
            input.ManualUpdate(true, new Vector2(8f, 0f), false);
            input.ManualUpdate(false, NoMove, false);

            Assert.IsFalse(input.TryConsumeShortPress(), "ちょうど閾値でドラッグになるため短押しは不成立");
        }

        [Test]
        public void 解放フレームに閾値以上のdeltaを与えると成立しない()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, NoMove, false);
            // 移動の大半が解放フレームに乗る高速フリックを再現
            // Reproduce a fast flick whose movement lands mostly on the release frame
            input.ManualUpdate(false, new Vector2(9f, 0f), false);

            Assert.IsFalse(input.TryConsumeShortPress(), "解放フレームのdeltaも累積判定に含めるため成立しない");
        }

        [Test]
        public void 押下フレームに閾値以上のdeltaを与えると成立しない()
        {
            var input = new RightShortPressInput();

            // 移動の大半が押下フレームに乗る高速フリックを再現
            // Reproduce a fast flick whose movement lands mostly on the press frame
            input.ManualUpdate(true, new Vector2(9f, 0f), false);
            input.ManualUpdate(false, NoMove, false);

            Assert.IsFalse(input.TryConsumeShortPress(), "押下フレームのdeltaも解放フレームと対称に累積するため成立しない");
        }

        [Test]
        public void 押下フレームの小さな移動は次の押下へ持ち越さない()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, new Vector2(5f, 0f), false);
            input.ManualUpdate(false, NoMove, false);
            Assert.IsTrue(input.TryConsumeShortPress());

            input.ManualUpdate(true, new Vector2(5f, 0f), false);
            input.ManualUpdate(false, NoMove, false);
            Assert.IsTrue(input.TryConsumeShortPress(), "累積は押下ごとに押下フレームのdeltaから始まる");
        }

        [Test]
        public void UI外で押してUI上で離すと成立しない()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, NoMove, false);
            input.ManualUpdate(false, NoMove, true);

            Assert.IsFalse(input.TryConsumeShortPress(), "解放時点がパネル上ならUI操作として扱う");
        }

        [Test]
        public void pollされていない押下中にResetすると離しても成立しない()
        {
            var input = new RightShortPressInput();

            // 他UIState滞在中に押し始めた押下を再現する（ManualUpdateを一度も通していない）
            // Reproduce a press started while another UIState was active, so ManualUpdate never observed it
            input.Reset(true);
            input.ManualUpdate(true, NoMove, false);
            input.ManualUpdate(false, NoMove, false);

            Assert.IsFalse(input.TryConsumeShortPress(), "Resetに渡した物理押下も死んだ押下として扱う");
        }
    }
}
