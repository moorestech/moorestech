using Client.Game.InGame.UI.UIState.State.CancelInput;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.CancelInput
{
    /// <summary>
    ///     マウスデバイス不在で移動量が計測できないフレームの扱いを固定する回帰試験
    ///     Regression tests pinning how frames whose mouse movement cannot be measured are treated
    /// </summary>
    public class RightShortPressInputDeltaMeasurementTest
    {
        private static readonly Vector2 NoMove = Vector2.zero;

        [Test]
        public void 計測不能フレームを挟んだ押下は離しても成立しない()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, true, NoMove, false);
            // マウスデバイス不在で移動量が読めないフレームを再現する
            // Reproduce a frame whose movement cannot be read because no mouse device exists
            input.ManualUpdate(true, false, NoMove, false);
            input.ManualUpdate(true, true, NoMove, false);
            input.ManualUpdate(false, true, NoMove, false);

            Assert.IsFalse(input.TryConsumeShortPress(), "計測不能フレームは移動0と区別できないため短押しにしない");

            input.ManualUpdate(true, true, NoMove, false);
            input.ManualUpdate(false, true, NoMove, false);
            Assert.IsTrue(input.TryConsumeShortPress(), "計測が戻った次の押下は成立する");
        }

        [Test]
        public void 計測不能なまま押し始めた押下は成立しない()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, false, NoMove, false);
            input.ManualUpdate(false, true, NoMove, false);

            Assert.IsFalse(input.TryConsumeShortPress(), "押下フレームが計測不能なら武装しない");
        }

        [Test]
        public void 離しフレームが計測不能なら成立しない()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, true, NoMove, false);
            input.ManualUpdate(false, false, NoMove, false);

            Assert.IsFalse(input.TryConsumeShortPress(), "離しフレームの移動量が読めないなら短押しと断定できない");
        }
    }
}
