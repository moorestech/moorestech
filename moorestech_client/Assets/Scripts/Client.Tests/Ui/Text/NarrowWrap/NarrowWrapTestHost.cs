using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Tests.Ui.Text.LineEndPunctuation;
using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Text.WrapSafe;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text.NarrowWrap
{
    /// <summary>
    /// 狭い幅の折り返しの回避（bd moorestech-fcpzz）の L1 の土台。固まる最小例の文・上限つきのフレームの回し方・回避のログの数え方を持つ
    /// L1 base for the narrow-width wrap guard (bd moorestech-fcpzz): the minimal freezing text, a frame-capped runner and a counter of the guard's logs
    /// </summary>
    /// <remarks>
    /// 回避が効かないと FrameUpdate が戻らず Editor ごと固まる（テストの失敗では止まらない。フレームの上限も FrameUpdate の中の無限ループは止めない）。上限の内に終わることを通ったことの証にし、固まった実行は TestWatchdog（既定 3 分）が Environment.FailFast で Editor ごと落として失敗にする（uloop・CI の batchmode とも同じ）
    /// Without the guard FrameUpdate never returns and the Editor hangs (no test failure stops it, and the frame cap cannot break a loop inside FrameUpdate); finishing within the cap is the pass, and a hung run fails because TestWatchdog (3 minutes by default) kills the Editor with Environment.FailFast (same under uloop and CI batchmode)
    /// </remarks>
    public abstract class NarrowWrapTestHost : LineEndPunctuationArrangementHost
    {
        // 「字＋開き括弧＋字」の最小の例。15px なら 31px 未満で折り返すと固まる
        // Minimal "glyph + opening bracket + glyph"; at 15px wrapping below 31px freezes
        protected const string MinimalFreezingText = "ん（前";
        protected const float FreezingWidth = 20f;
        private const int FrameCap = 12;
        private static readonly Regex HoldWarning = new(@"^\[NarrowWrapGuard\] .* stops wrapping");
        private static readonly Regex ReleaseLog = new(@"^\[NarrowWrapGuard\] .* wraps again");

        private readonly List<LogType> _guardLogs = new();

        protected int HoldWarnings { get; private set; }
        protected int ReleaseLogs { get; private set; }

        [SetUp]
        public void CountGuardLogs()
        {
            HoldWarnings = 0;
            ReleaseLogs = 0;
            _guardLogs.Clear();
            Application.logMessageReceived += OnLog;
        }

        [TearDown]
        public void StopCountingGuardLogs()
        {
            Application.logMessageReceived -= OnLog;
        }

        protected void RunCappedFrames()
        {
            for (var frame = 0; frame < FrameCap; frame++) simulate.FrameUpdate();
        }

        // 素のラベル（WrapSafeLabel）か言語追従ラベルを、通知の行の文字組みで配置の流れに載る形で作る
        // Create a plain WrapSafeLabel or a LocalizedLabel in the notification row typography, joining the layout flow
        protected static Label CreateLabel(bool localized, string text)
        {
            Label label = localized ? new LocalizedLabel() : new WrapSafeLabel(text);
            if (label is LocalizedLabel localizedLabel) localizedLabel.SetText(UiText.FromVerbatim(text));
            Style(label, 0f);
            label.style.position = Position.Relative;
            return label;
        }

        protected static void AssertHoldsNoWrap(Label label, string context)
        {
            Assert.AreEqual(WhiteSpace.NoWrap, label.resolvedStyle.whiteSpace, context);
            Assert.IsFalse(label.text.Contains("\n"), $"{context}: {label.text}");
        }

        // 受けた回避のログを受けた順に想定済みにする（想定外の警告・ログでテストが落ち、想定は順に照合される）
        // Mark the received guard logs as expected in arrival order (unexpected warnings and logs fail tests, and expectations match in order)
        protected void ExpectCountedGuardLogs()
        {
            foreach (var type in _guardLogs) LogAssert.Expect(type, type == LogType.Warning ? HoldWarning : ReleaseLog);
        }

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Warning && HoldWarning.IsMatch(message))
            {
                HoldWarnings++;
                _guardLogs.Add(type);
            }
            if (type == LogType.Log && ReleaseLog.IsMatch(message))
            {
                ReleaseLogs++;
                _guardLogs.Add(type);
            }
        }
    }
}
