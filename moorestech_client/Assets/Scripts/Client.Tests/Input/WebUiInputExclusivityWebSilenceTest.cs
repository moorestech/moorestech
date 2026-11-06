using System.Diagnostics;
using Client.Input;
using NUnit.Framework;

namespace Client.Tests.Input
{
    // WSの受信が閾値を超えて途絶えたら入力排他はWeb側の値を読まない（plan §3.11）。印は評価だけが動かす
    // Once WS receipts stop past the threshold, input exclusivity stops reading the web side (plan §3.11); only evaluation moves the mark
    public class WebUiInputExclusivityWebSilenceTest
    {
        private static readonly long BaseTimestamp = Stopwatch.Frequency * 1000;

        [SetUp]
        public void SetUp()
        {
            WebUiInputExclusivity.ResetWebSilenceForTest();
        }

        [TearDown]
        public void TearDown()
        {
            WebUiInputExclusivity.ResetWebSilenceForTest();
            WebUiInputExclusivity.SetState(false, false);
        }

        [Test]
        public void 閾値を超えて受信が途絶えるとWebの値を無視し受信と評価で戻る()
        {
            WebUiInputExclusivity.RecordWebMessageReceived(BaseTimestamp);
            WebUiInputExclusivity.SetState(true, true);

            // 14秒では閾値内なのでWebの値が効く
            // At 14s the web is within the threshold, so its values count
            WebUiInputExclusivity.EvaluateWebSilence(At(14));
            AssertWebValuesCount(true);

            // 16秒で無応答、Web側を読まない
            // At 16s web is silent; web side is not read
            WebUiInputExclusivity.EvaluateWebSilence(At(16));
            AssertWebValuesCount(false);

            // 受信が戻っても評価までは印が残る
            // After a receipt the mark stays until the next evaluation
            WebUiInputExclusivity.RecordWebMessageReceived(At(17));
            AssertWebValuesCount(false);

            WebUiInputExclusivity.EvaluateWebSilence(At(18));
            AssertWebValuesCount(true);
        }

        [Test]
        public void 一度も受信していなければ無視しない()
        {
            WebUiInputExclusivity.SetState(true, true);
            WebUiInputExclusivity.EvaluateWebSilence(At(100));

            Assert.IsNull(WebUiInputExclusivity.SecondsSinceLastWebMessage(At(100)));
            AssertWebValuesCount(true);
        }

        [Test]
        public void 受信からの秒数をStopwatchの周波数で換算する()
        {
            WebUiInputExclusivity.RecordWebMessageReceived(BaseTimestamp);
            Assert.AreEqual(2.5, WebUiInputExclusivity.SecondsSinceLastWebMessage(BaseTimestamp + Stopwatch.Frequency * 5 / 2).Value, 1e-9);
        }

        [Test]
        public void 時刻0の受信も受信として扱う()
        {
            // 未受信は時刻の値でなく有無で持つので、時刻0でも受信済みになり無応答判定が働く
            // Never-received is held as absence rather than a time value, so a receipt at time 0 still counts and silence is judged
            WebUiInputExclusivity.RecordWebMessageReceived(0);
            WebUiInputExclusivity.SetState(true, true);
            Assert.AreEqual(16.0, WebUiInputExclusivity.SecondsSinceLastWebMessage(Stopwatch.Frequency * 16).Value, 1e-9);

            WebUiInputExclusivity.EvaluateWebSilence(Stopwatch.Frequency * 16);
            AssertWebValuesCount(false);
        }

        private static long At(double secondsAfterBase)
        {
            return BaseTimestamp + (long)(Stopwatch.Frequency * secondsAfterBase);
        }

        private static void AssertWebValuesCount(bool expected)
        {
            Assert.AreEqual(expected, WebUiInputExclusivity.IsPointerOverWebUi);
            Assert.AreEqual(expected, WebUiInputExclusivity.IsTextInputFocused);
            Assert.AreEqual(expected, WebUiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));
        }
    }
}
