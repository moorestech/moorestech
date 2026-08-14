using Client.Game.InGame.Hotbar;
using NUnit.Framework;

namespace Client.Tests.Hotbar
{
    /// <summary>
    ///     数字キーのタップ/長押し判別とUIState遷移時のReset契約の回帰試験
    ///     Regression tests for tap/long-press classification and the Reset contract used on UIState transitions
    /// </summary>
    public class HotbarKeyInputTest
    {
        private const float LongPressThresholdSeconds = 0.5f;
        private const int PressedSlot = 2;

        [Test]
        public void ReleaseBeforeThresholdIsATapAndIsConsumedOnce()
        {
            var keyInput = new HotbarKeyInput();

            keyInput.ManualUpdate(PressedSlot, 0f);
            keyInput.ManualUpdate(PressedSlot, LongPressThresholdSeconds - 0.01f);
            keyInput.ManualUpdate(null, LongPressThresholdSeconds - 0.01f);

            Assert.IsTrue(keyInput.TryGetTappedSlot(out var tappedSlot));
            Assert.AreEqual(PressedSlot, tappedSlot);
            Assert.IsFalse(keyInput.TryGetTappedSlot(out _), "タップは1度だけ消費される");
            Assert.IsFalse(keyInput.TryGetLongPressedSlot(out _), "閾値未満の離しは長押しにならない");
        }

        [Test]
        public void HoldPastThresholdFiresLongPressOnceAndSuppressesTheTap()
        {
            var keyInput = new HotbarKeyInput();

            keyInput.ManualUpdate(PressedSlot, 0f);
            keyInput.ManualUpdate(PressedSlot, LongPressThresholdSeconds);

            Assert.IsTrue(keyInput.TryGetLongPressedSlot(out var longPressedSlot));
            Assert.AreEqual(PressedSlot, longPressedSlot);

            // 保持継続でも再発火せず、成立後の離しはタップにならない
            // Holding further never re-fires, and the release after a fired long press is not a tap
            keyInput.ManualUpdate(PressedSlot, LongPressThresholdSeconds + 1f);
            keyInput.ManualUpdate(null, LongPressThresholdSeconds + 1f);

            Assert.IsFalse(keyInput.TryGetLongPressedSlot(out _));
            Assert.IsFalse(keyInput.TryGetTappedSlot(out _));
        }

        [Test]
        public void ResetDiscardsPendingTapAndStalePressStartTime()
        {
            var keyInput = new HotbarKeyInput();

            // 押して離した直後（タップ保留中）にUIStateが遷移した状況
            // A UIState transition right after a press-release, while a tap is still pending
            keyInput.ManualUpdate(PressedSlot, 0f);
            keyInput.ManualUpdate(null, 0.1f);
            keyInput.Reset();

            Assert.IsFalse(keyInput.TryGetTappedSlot(out _), "遷移前のタップは持ち越さない");

            // 他UIState滞在中に時間だけ進み、復帰直後も押しっぱなしだった場合
            // Time advances while another UIState is active and the key is still held on return
            keyInput.ManualUpdate(PressedSlot, 10f);
            keyInput.ManualUpdate(PressedSlot, 10.1f);

            Assert.IsFalse(keyInput.TryGetLongPressedSlot(out _), "復帰直後の押下は復帰時刻から計り直す");
        }
    }
}
