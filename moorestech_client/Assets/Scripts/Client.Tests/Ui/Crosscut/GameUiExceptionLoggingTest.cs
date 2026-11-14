using System;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Game.InGame.BugReport;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Crosscut
{
    // AppErrorBoundary の代わりの前提: UI の例外は握られずに LogType.Exception で出て、バグ報告のログ環が拾う
    // Premise standing in for AppErrorBoundary: UI exceptions are never swallowed, surface as LogType.Exception and land in the bug-report log ring
    public class GameUiExceptionLoggingTest : Client.Tests.Ui.Screens.ScreenTestHost
    {
        [Test]
        public void UIのクリックの中の例外はExceptionでログに出て環に入る()
        {
            var ring = new UnityLogRing();
            ring.Initialize();
            var button = Mount(new ThrowingButton());
            LogAssert.Expect(LogType.Exception, new Regex("GameUiExceptionProbe"));
            simulate.Click(button.worldBound.center);
            var entry = ring.Dump().Last(log => log.Type == LogType.Exception);
            StringAssert.Contains("GameUiExceptionProbe", entry.Message);
            StringAssert.Contains("Client.Tests.Ui.Crosscut", entry.StackTrace);
        }

        [Test]
        public void UniRxの購読の中の例外はExceptionでログに出る()
        {
            var ring = new UnityLogRing();
            ring.Initialize();
            var subject = new Subject<Unit>();
            subject.Subscribe(_ => throw new InvalidOperationException("GameUiExceptionProbe rx"));
            LogAssert.Expect(LogType.Exception, new Regex("GameUiExceptionProbe rx"));
            // UniRx は onError 未指定の例外を投げ直す。UI のイベント配送の中で投げ直されたものは UITK がログへ出す
            // UniRx rethrows when no onError is given; rethrown inside UI event dispatch, UITK logs it
            var button = Mount(new Button(() => subject.OnNext(Unit.Default)) { name = "rx-probe" });
            simulate.Click(button.worldBound.center);
            Assert.IsTrue(ring.Dump().Any(log => log.Type == LogType.Exception && log.Message.Contains("GameUiExceptionProbe rx")));
        }

        private sealed class ThrowingButton : Button
        {
            public ThrowingButton() : base(() => throw new InvalidOperationException("GameUiExceptionProbe click"))
            {
                name = "exception-probe";
                style.width = 40;
                style.height = 40;
            }
        }
    }
}
