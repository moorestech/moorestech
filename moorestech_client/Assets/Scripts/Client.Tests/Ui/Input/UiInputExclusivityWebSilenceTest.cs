using System.Diagnostics;
using Client.Input;
using NUnit.Framework;

namespace Client.Tests.Ui.Input
{
    // WSの受信が閾値を超えて途絶えたら入力排他はWeb側の値を読まない（§3.11）。印は評価だけが動かす（D-22）
    // Once WS receipts stop past the threshold, input exclusivity stops reading the web side (§3.11); only evaluation moves the mark (D-22)
    public class UiInputExclusivityWebSilenceTest
    {
        private static readonly long BaseTimestamp = Stopwatch.Frequency * 1000;

        [SetUp]
        public void SetUp()
        {
            UiInputExclusivity.ResetWebSilenceForTest();
        }

        [TearDown]
        public void TearDown()
        {
            UiInputExclusivity.ResetWebSilenceForTest();
            UiInputExclusivity.SetWebUiSuspended(false);
            UiInputExclusivity.SetState(false, false);
        }

        [Test]
        public void 閾値を超えて受信が途絶えるとWebの値を無視し受信と評価で戻る()
        {
            UiInputExclusivity.RecordWebMessageReceived(BaseTimestamp);
            UiInputExclusivity.SetState(true, true);

            // 14秒では閾値内なのでWebの値が効く
            // At 14s the web is within the threshold, so its values count
            UiInputExclusivity.EvaluateWebSilence(At(14));
            AssertWebValuesCount(true);

            // 16秒で無応答とみなし、ポインタも文字入力もWeb側を読まない
            // At 16s the web counts as silent; neither pointer nor text input reads the web side
            UiInputExclusivity.EvaluateWebSilence(At(16));
            AssertWebValuesCount(false);

            // 受信が戻っても評価までは印が残る
            // After a receipt the mark stays until the next evaluation
            UiInputExclusivity.RecordWebMessageReceived(At(17));
            AssertWebValuesCount(false);

            UiInputExclusivity.EvaluateWebSilence(At(18));
            AssertWebValuesCount(true);
        }

        [Test]
        public void 一度も受信していなければ無視しない()
        {
            UiInputExclusivity.SetState(true, true);
            UiInputExclusivity.EvaluateWebSilence(At(100));

            Assert.IsNull(UiInputExclusivity.SecondsSinceLastWebMessage(At(100)));
            AssertWebValuesCount(true);
        }

        [Test]
        public void 受信からの秒数をStopwatchの周波数で換算する()
        {
            UiInputExclusivity.RecordWebMessageReceived(BaseTimestamp);
            Assert.AreEqual(2.5, UiInputExclusivity.SecondsSinceLastWebMessage(BaseTimestamp + Stopwatch.Frequency * 5 / 2).Value, 1e-9);
        }

        // 休止中に受信が止まっても、解除の時点で無応答ならWebの値は効かない
        // Even if receipts stop during suspension, a web still silent at resume has no effect
        [Test]
        public void 休止とは独立に無応答を判定する()
        {
            UiInputExclusivity.RecordWebMessageReceived(BaseTimestamp);
            UiInputExclusivity.SetWebUiSuspended(true);
            UiInputExclusivity.EvaluateWebSilence(At(20));
            UiInputExclusivity.SetWebUiSuspended(false);

            UiInputExclusivity.SetState(true, true);
            AssertWebValuesCount(false);

            UiInputExclusivity.RecordWebMessageReceived(At(21));
            UiInputExclusivity.EvaluateWebSilence(At(22));
            AssertWebValuesCount(true);
        }

        private static long At(double secondsAfterBase)
        {
            return BaseTimestamp + (long)(Stopwatch.Frequency * secondsAfterBase);
        }

        private static void AssertWebValuesCount(bool expected)
        {
            Assert.AreEqual(expected, UiInputExclusivity.IsPointerOverWebUi);
            Assert.AreEqual(expected, UiInputExclusivity.IsTextInputFocused);
            Assert.AreEqual(expected, UiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));
        }
    }
}
