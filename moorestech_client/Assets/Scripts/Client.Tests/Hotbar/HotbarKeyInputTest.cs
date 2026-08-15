using Client.Game.InGame.Hotbar;
using NUnit.Framework;

namespace Client.Tests.Hotbar
{
    /// <summary>
    ///     タップ/長押し判別とReset契約の回帰試験
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

            // 保持継続で再発火なし
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

            // タップ保留中にUIState遷移
            // A UIState transition right after a press-release, while a tap is still pending
            keyInput.ManualUpdate(PressedSlot, 0f);
            keyInput.ManualUpdate(null, 0.1f);
            keyInput.Reset();

            Assert.IsFalse(keyInput.TryGetTappedSlot(out _), "遷移前のタップは持ち越さない");

            // 他UIState中に時間経過、押下継続
            // Time advances while another UIState is active and the key is still held on return
            keyInput.ManualUpdate(PressedSlot, 10f);
            keyInput.ManualUpdate(PressedSlot, 10.1f);

            Assert.IsFalse(keyInput.TryGetLongPressedSlot(out _), "復帰直後の押下は復帰時刻から計り直す");
        }

        [Test]
        public void ResetWhileHeldNeverRearmsUntilTheKeyIsReleased()
        {
            var keyInput = new HotbarKeyInput();

            // 長押し成立で割当てた直後、押しっぱなしのままUIStateが遷移する
            // The long press assigns, then a UIState transition happens while the key is still held down
            keyInput.ManualUpdate(PressedSlot, 0f);
            keyInput.ManualUpdate(PressedSlot, LongPressThresholdSeconds);
            Assert.IsTrue(keyInput.TryGetLongPressedSlot(out _));
            keyInput.Reset();

            // 押下継続は再武装しない。閾値を跨いでも2度目の長押しもタップも出ない
            // The continued hold never re-arms: neither a second long press nor a tap appears across the threshold
            keyInput.ManualUpdate(PressedSlot, LongPressThresholdSeconds + 0.1f);
            keyInput.ManualUpdate(PressedSlot, LongPressThresholdSeconds * 3f);

            Assert.IsFalse(keyInput.TryGetLongPressedSlot(out _), "1回の長押しは2度割当てない");
            Assert.IsFalse(keyInput.TryGetTappedSlot(out _));

            // 離して押し直せば通常どおりタップが成立する
            // Releasing and pressing again classifies as a tap exactly as usual
            keyInput.ManualUpdate(null, LongPressThresholdSeconds * 3f);
            Assert.IsFalse(keyInput.TryGetTappedSlot(out _), "死格化された押下の解放はタップにならない");

            keyInput.ManualUpdate(PressedSlot, 10f);
            keyInput.ManualUpdate(null, 10.1f);
            Assert.IsTrue(keyInput.TryGetTappedSlot(out var tappedSlot));
            Assert.AreEqual(PressedSlot, tappedSlot);
        }
    }
}
