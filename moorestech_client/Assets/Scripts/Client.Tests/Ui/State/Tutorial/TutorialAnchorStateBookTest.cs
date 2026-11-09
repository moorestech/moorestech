using Client.Ui.State.Tutorial.Logic;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Tutorial
{
    /// <summary>
    /// アンカーの状態の帳簿が、同値の再解決を捨て、clip の変化と購読外しを拾うことを確かめる（L0）
    /// Verifies the anchor state book drops equal re-resolutions and notices clip changes and unsubscriptions (L0)
    /// </summary>
    public class TutorialAnchorStateBookTest
    {
        private static readonly TutorialClipRect Viewport = new(0, 0, 1280, 720);

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">同値rectの再解決では再描画しない</web-test>
        [Test]
        public void 同じ矩形とclipの再解決は変化として扱わない()
        {
            var book = new TutorialAnchorStateBook();
            Assert.IsTrue(book.ApplyIfChanged("pause.save", new TutorialAnchorState.Ready(new TutorialClipRect(10, 20, 110, 60), Viewport)));
            Assert.IsFalse(book.ApplyIfChanged("pause.save", new TutorialAnchorState.Ready(new TutorialClipRect(10, 20, 110, 60), Viewport)));
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">rectが同値でもclipが変われば再描画する</web-test>
        [Test]
        public void 矩形が同じでもclipが変われば変化として扱う()
        {
            var book = new TutorialAnchorStateBook();
            var rect = new TutorialClipRect(10, 20, 110, 60);
            book.ApplyIfChanged("research.node-a", new TutorialAnchorState.Ready(rect, Viewport));
            Assert.IsTrue(book.ApplyIfChanged("research.node-a", new TutorialAnchorState.Ready(rect, new TutorialClipRect(0, 30, 1280, 720))));
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">購読対象外になったanchorの解決状態は落とす</web-test>
        [Test]
        public void 購読から外れたアンカーの状態は捨てる()
        {
            var book = new TutorialAnchorStateBook();
            book.ApplyIfChanged("pause.save", new TutorialAnchorState.Ready(new TutorialClipRect(0, 0, 10, 10), Viewport));
            book.ApplyIfChanged("pause.back", TutorialAnchorState.NotFound.Missing());
            book.KeepOnly(new[] { "pause.back" });

            Assert.IsFalse(book.TryGet("pause.save", out _));
            Assert.IsTrue(book.TryGet("pause.back", out var kept));
            Assert.IsInstanceOf<TutorialAnchorState.NotFound>(kept);
        }

        [Test]
        public void 理由だけが変わった未解決も変化として扱う()
        {
            var book = new TutorialAnchorStateBook();
            book.ApplyIfChanged("pause.save", TutorialAnchorState.NotFound.Missing());
            Assert.IsTrue(book.ApplyIfChanged("pause.save", TutorialAnchorState.NotFound.DuplicateAnchor()));
        }
    }
}
