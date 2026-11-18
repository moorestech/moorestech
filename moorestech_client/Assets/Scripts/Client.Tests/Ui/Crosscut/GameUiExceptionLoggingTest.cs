using System;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Crosscut
{
    // AppErrorBoundary の代わりの前提: UI の例外は配送が握らず投げ直す。本番では PlayerLoop が LogType.Exception でログへ出し、数え手が拾う
    // Premise standing in for AppErrorBoundary: UI dispatch never swallows exceptions but rethrows them; in production the PlayerLoop logs them as LogType.Exception for the tally
    public class GameUiExceptionLoggingTest : Client.Tests.Ui.Screens.ScreenTestHost
    {
        [Test]
        public void UIのクリックの中の例外は配送が握らず投げ直す()
        {
            var button = Mount(new ThrowingButton());
            var exception = Assert.Throws<InvalidOperationException>(() => simulate.Click(button.worldBound.center));
            StringAssert.Contains("GameUiExceptionProbe click", exception.Message);
            StringAssert.Contains("Client.Tests.Ui.Crosscut", exception.StackTrace);
        }

        [Test]
        public void UniRxの購読の中の例外もUIの配送が握らず投げ直す()
        {
            var subject = new Subject<Unit>();
            subject.Subscribe(_ => throw new InvalidOperationException("GameUiExceptionProbe rx"));
            // UniRx は onError 未指定の例外を投げ直し、UITK の配送もそれを握らない
            // UniRx rethrows when no onError is given, and UITK dispatch does not swallow it either
            var button = Mount(new Button(() => subject.OnNext(Unit.Default)) { name = "rx-probe" });
            var exception = Assert.Throws<InvalidOperationException>(() => simulate.Click(button.worldBound.center));
            StringAssert.Contains("GameUiExceptionProbe rx", exception.Message);
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
