using Client.Playtest.UiElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.Dsl
{
    /// <summary>
    /// 両系合成の優先順・重複・既存 Web シナリオの不変を偽の 2 ロケータで固定する（L0）
    /// Pins the composite's priority, duplicate handling and unchanged Web-only scenarios with two fake locators (L0)
    /// </summary>
    public class CompositeUiElementLocatorTest
    {
        private const string Testid = "dsl-target";

        [Test]
        public void WebOnlyScenarioReturnsWebSnapshotWhileGameUiIsEmpty()
        {
            // 段階 1 の GameUi は層が空で NotFound、Web だけが Found を返す今のシナリオは同じ結果になる
            // In stage 1 GameUi has empty layers and answers NotFound, so today's Web-only scenarios keep the Web result
            var gameUi = Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.NotFound);
            var web = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var composite = new CompositeUiElementLocator(gameUi, web);

            var snapshot = Find(composite);
            Assert.AreSame(web.LastFound, snapshot);
            Assert.AreEqual(UiSetTextStatus.Applied, SetText(composite).Status);
            Assert.AreEqual(1, web.SetTextCount);
        }

        [Test]
        public void GameUiHitIsPreferredAndWebIsNotWritten()
        {
            var gameUi = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var web = Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.NotFound);
            var composite = new CompositeUiElementLocator(gameUi, web);

            var snapshot = Find(composite);
            Assert.AreSame(gameUi.LastFound, snapshot);
            Assert.AreEqual(UiSetTextStatus.Applied, SetText(composite).Status);
            Assert.AreEqual(0, web.SetTextCount);
        }

        [Test]
        public void FoundInBothIsAmbiguousWithError()
        {
            var composite = new CompositeUiElementLocator(
                Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied),
                Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied));

            LogAssert.Expect(LogType.Error, $"[PlaytestUi] testid '{Testid}' resolved in both game UI and web UI");
            Assert.AreEqual(UiElementLookupStatus.Ambiguous, Find(composite).Status);
        }

        [Test]
        public void AmbiguousOnOneSideIsKept()
        {
            var composite = new CompositeUiElementLocator(
                Fake(UiElementLookupStatus.Ambiguous, UiSetTextStatus.Ambiguous),
                Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.NotFound));

            Assert.AreEqual(UiElementLookupStatus.Ambiguous, Find(composite).Status);
            Assert.AreEqual(UiSetTextStatus.Ambiguous, SetText(composite).Status);

            // 他系で Found でも、重複のある系がある限り Ambiguous
            // Still Ambiguous while one side has duplicates, even if the other side has a hit
            var withWebHit = new CompositeUiElementLocator(
                Fake(UiElementLookupStatus.Ambiguous, UiSetTextStatus.Ambiguous),
                Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied));
            Assert.AreEqual(UiElementLookupStatus.Ambiguous, Find(withWebHit).Status);
        }

        [Test]
        public void NeitherFoundReturnsNotFoundWhenBothCanAnswer()
        {
            var composite = new CompositeUiElementLocator(
                Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.NotFound),
                Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.NotFound));

            Assert.AreEqual(UiElementLookupStatus.NotFound, Find(composite).Status);
            Assert.AreEqual(UiSetTextStatus.NotFound, SetText(composite).Status);
        }

        [Test]
        public void GameUiNotFoundWithWebUnavailableReturnsRendererUnavailable()
        {
            // Web が未準備なら NotFound に化けさせず、期限切れ例外の last: に未準備が出るようにする
            // Do not disguise an unready Web as NotFound, so the deadline exception's last: shows it
            var composite = new CompositeUiElementLocator(
                Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.NotFound),
                Fake(UiElementLookupStatus.RendererUnavailable, UiSetTextStatus.RendererUnavailable));

            Assert.AreEqual(UiElementLookupStatus.RendererUnavailable, Find(composite).Status);
            Assert.AreEqual(UiSetTextStatus.RendererUnavailable, SetText(composite).Status);
        }

        [Test]
        public void NeitherFoundReturnsWebResultWhenGameUiIsUnavailable()
        {
            var composite = new CompositeUiElementLocator(
                Fake(UiElementLookupStatus.RendererUnavailable, UiSetTextStatus.RendererUnavailable),
                Fake(UiElementLookupStatus.RendererUnavailable, UiSetTextStatus.RendererUnavailable));

            Assert.AreEqual(UiElementLookupStatus.RendererUnavailable, Find(composite).Status);
        }

        [Test]
        public void GameUiRefusalIsReturnedWithoutTryingWeb()
        {
            // GameUi にある欄が入力を拒んだら Web の同名欄へ流さない
            // When a GameUi field refuses the input, do not fall through to a Web field of the same name
            var web = Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.Applied);
            var composite = new CompositeUiElementLocator(Fake(UiElementLookupStatus.Found, UiSetTextStatus.NotInteractable), web);

            Assert.AreEqual(UiSetTextStatus.NotInteractable, SetText(composite).Status);
            Assert.AreEqual(0, web.SetTextCount);
        }

        [Test]
        public void SessionReachesBoth()
        {
            var gameUi = Fake(UiElementLookupStatus.RendererUnavailable, UiSetTextStatus.RendererUnavailable);
            var web = Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.NotFound);
            var composite = new CompositeUiElementLocator(gameUi, web);

            composite.StartSession();
            composite.StopSession();
            Assert.AreEqual((1, 1), (gameUi.StartCount, gameUi.StopCount));
            Assert.AreEqual((1, 1), (web.StartCount, web.StopCount));
        }

        private static FakeUiElementLocator Fake(UiElementLookupStatus findStatus, UiSetTextStatus setTextStatus)
        {
            return new FakeUiElementLocator(findStatus, setTextStatus);
        }

        // 偽は同期に完了するので、その場で結果を取り出せる
        // The fakes complete synchronously, so the result can be read immediately
        private static UiElementSnapshot Find(CompositeUiElementLocator composite)
        {
            return composite.Find(Testid, 1f).GetAwaiter().GetResult();
        }

        private static UiSetTextResult SetText(CompositeUiElementLocator composite)
        {
            return composite.SetText(Testid, "text", 1f).GetAwaiter().GetResult();
        }
    }
}
