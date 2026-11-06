using Client.Input;
using Client.Playtest.UiElements;
using NUnit.Framework;

namespace Client.Tests.Ui.Dsl
{
    /// <summary>
    /// 選ばれた系だけで探すことを固定する
    /// Pins that only the chosen system is used for lookups
    /// </summary>
    public class SwitchedUiElementLocatorTest
    {
        private const string Testid = "dsl-target";

        [Test]
        public void GameUi系はGameUiの結果を返しWebに照会しない()
        {
            var gameUi = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var web = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var locator = new SwitchedUiElementLocator(UiSystem.GameUi, gameUi, web);

            var found = Find(locator);
            Assert.AreSame(gameUi.LastFound, found);
            Assert.AreEqual(0, web.FindCount, "GameUi 系で Web に照会した");
        }

        [Test]
        public void GameUi系でGameUiに無ければWebへ回さずNotFound()
        {
            var web = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var locator = new SwitchedUiElementLocator(UiSystem.GameUi, Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.NotFound), web);

            Assert.AreEqual(UiElementLookupStatus.NotFound, Find(locator).Status);
            Assert.AreEqual(0, web.FindCount);
        }

        [Test]
        public void GameUi系の未準備はそのまま返す()
        {
            var web = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var locator = new SwitchedUiElementLocator(UiSystem.GameUi, Fake(UiElementLookupStatus.RendererUnavailable, UiSetTextStatus.RendererUnavailable), web);

            Assert.AreEqual(UiElementLookupStatus.RendererUnavailable, Find(locator).Status);
            Assert.AreEqual(UiSetTextStatus.RendererUnavailable, SetText(locator).Status);
            Assert.AreEqual((0, 0), (web.FindCount, web.SetTextCount));
        }

        [Test]
        public void GameUi系の文字入力はGameUiだけに入れる()
        {
            var gameUi = Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.NotFound);
            var web = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var locator = new SwitchedUiElementLocator(UiSystem.GameUi, gameUi, web);

            Assert.AreEqual(UiSetTextStatus.NotFound, SetText(locator).Status);
            Assert.AreEqual((1, 0), (gameUi.SetTextCount, web.SetTextCount));
        }

        [Test]
        public void Web系はWebの結果を返しGameUiに照会しない()
        {
            var gameUi = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var web = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var locator = new SwitchedUiElementLocator(UiSystem.Web, gameUi, web);

            var found = Find(locator);
            Assert.AreSame(web.LastFound, found);
            Assert.AreEqual(0, gameUi.FindCount, "Web 系で GameUi に照会した");
        }

        [Test]
        public void Web系でWebに無ければGameUiへ回さずNotFound()
        {
            var gameUi = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var locator = new SwitchedUiElementLocator(UiSystem.Web, gameUi, Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.NotFound));

            Assert.AreEqual(UiElementLookupStatus.NotFound, Find(locator).Status);
            Assert.AreEqual(0, gameUi.FindCount);
        }

        [Test]
        public void Web系の文字入力はWebだけに入れる()
        {
            var gameUi = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var web = Fake(UiElementLookupStatus.Found, UiSetTextStatus.NotInteractable);
            var locator = new SwitchedUiElementLocator(UiSystem.Web, gameUi, web);

            Assert.AreEqual(UiSetTextStatus.NotInteractable, SetText(locator).Status);
            Assert.AreEqual((0, 1), (gameUi.SetTextCount, web.SetTextCount));
        }

        [Test]
        public void 選んだ系の重複はAmbiguousのまま返す()
        {
            var gameUiSide = new SwitchedUiElementLocator(UiSystem.GameUi, Fake(UiElementLookupStatus.Ambiguous, UiSetTextStatus.Ambiguous), Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied));
            var webSide = new SwitchedUiElementLocator(UiSystem.Web, Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied), Fake(UiElementLookupStatus.Ambiguous, UiSetTextStatus.Ambiguous));

            Assert.AreEqual(UiElementLookupStatus.Ambiguous, Find(gameUiSide).Status);
            Assert.AreEqual(UiSetTextStatus.Ambiguous, SetText(gameUiSide).Status);
            Assert.AreEqual(UiElementLookupStatus.Ambiguous, Find(webSide).Status);
        }

        [Test]
        public void 照会の残り時間をそのまま選んだ系へ渡す()
        {
            // 段階 1 の「GameUi で見つかった後の Web 照会を 0.5 秒で切る」は両系照会が無くなり要らない
            // Stage 1's 0.5s cut of the web duplicate check is gone with the two-sided lookup
            var gameUi = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            new SwitchedUiElementLocator(UiSystem.GameUi, gameUi, Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.NotFound)).Find(Testid, 3f).GetAwaiter().GetResult();

            Assert.AreEqual(3f, gameUi.LastFindBudgetSeconds);
        }

        [Test]
        public void セッションは選んだ系だけを開閉する()
        {
            var gameUi = Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.NotFound);
            var web = Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.NotFound);
            var gameUiSide = new SwitchedUiElementLocator(UiSystem.GameUi, gameUi, web);
            gameUiSide.StartSession();
            gameUiSide.StopSession();
            Assert.AreEqual((1, 1, 0, 0), (gameUi.StartCount, gameUi.StopCount, web.StartCount, web.StopCount));

            var webSide = new SwitchedUiElementLocator(UiSystem.Web, gameUi, web);
            webSide.StartSession();
            webSide.StopSession();
            Assert.AreEqual((1, 1, 1, 1), (gameUi.StartCount, gameUi.StopCount, web.StartCount, web.StopCount));
        }

        private static FakeUiElementLocator Fake(UiElementLookupStatus findStatus, UiSetTextStatus setTextStatus)
        {
            return new FakeUiElementLocator(findStatus, setTextStatus);
        }

        // 偽は同期に完了するので、その場で結果を取り出せる
        // The fakes complete synchronously, so the result can be read immediately
        private static UiElementSnapshot Find(SwitchedUiElementLocator locator)
        {
            return locator.Find(Testid, 1f).GetAwaiter().GetResult();
        }

        private static UiSetTextResult SetText(SwitchedUiElementLocator locator)
        {
            return locator.SetText(Testid, "text", 1f).GetAwaiter().GetResult();
        }
    }
}
