using Client.Localization;
using Client.Tests.Ui.TestDoubles;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.PauseMenu;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.PauseMenu
{
    /// <summary>
    /// ポーズの子画面の本文の basis が中身の高さに追従し、溢れると戻るが行の箱まで縮むことを確かめる（Web の ScrollArea の flex: 1 1 0% の解き方）
    /// Checks that the pause sub-page body basis follows its content height and Back shrinks to its line box on overflow (how the Web resolves ScrollArea flex: 1 1 0%)
    /// </summary>
    public class PauseMenuSubPageFlexTest : UiTestHost
    {
        private const float HostMaxHeight = 300f;
        private const float StackGapMd = 16f;
        private const float WebLineHeight = 1.55f;
        private const float PixelSnapTolerance = 1f;

        private VisualElement _section;
        private GameScrollView _body;
        private Button _back;

        [SetUp]
        public void MountSubPage()
        {
            // 戻るの文は辞書で解くので、単独で流しても読めるよう先に辞書を読む
            // Back resolves its text from the dictionary, so load it first even when run alone
            Localize.Initialize();
            // 高さを内容で決め max-height だけを持つ枠（Web の .panel と同じ形）に子画面を載せる
            // Mount the sub-page in a content-sized frame with only a max-height (the shape of the Web .panel)
            var host = new VisualElement();
            host.style.width = 300f;
            host.style.maxHeight = HostMaxHeight;
            host.style.flexDirection = FlexDirection.Column;
            var subPage = new PauseMenuSubPageView(new RecordingPauseMenuCommands(), new UiOsCursorRequestStack(new FakeUiOsCursorApplier()));
            _section = new VisualElement();
            _section.style.flexShrink = 0f;
            subPage.AddSection(_section);
            host.Add(subPage);
            rootVisualElement.Add(host);
            _body = subPage.Q<GameScrollView>("pause-menu-body");
            _back = subPage.Q<Button>("pause-menu-back");
        }

        [Test]
        public void 溢れないうちは本文のbasisが中身の高さで戻るは本来の高さ()
        {
            SetSectionHeight(100f);
            Assert.AreEqual(100f, _body.resolvedStyle.flexBasis.value, PixelSnapTolerance);
            Assert.AreEqual(100f, _body.layout.height, PixelSnapTolerance);
            Assert.Greater(_back.layout.height, _back.resolvedStyle.minHeight.value + PixelSnapTolerance, "Back keeps its own height");
        }

        [Test]
        public void 溢れると戻るは字の行の箱まで縮み中身が縮むと本文のbasisも戻る()
        {
            // 溢れたとき: 戻るは min-height（字の大きさ×1.55）で止まり、残りを本文が受ける
            // On overflow Back stops at its min-height (font size x 1.55) and the body takes the rest
            SetSectionHeight(600f);
            var lineBox = _back.resolvedStyle.fontSize * WebLineHeight;
            Assert.AreEqual(lineBox, _back.resolvedStyle.minHeight.value, 0.01f, "--panel-action-button-min-content-height = font size x --mantine-line-height");
            Assert.AreEqual(lineBox, _back.layout.height, PixelSnapTolerance);
            Assert.AreEqual(HostMaxHeight - StackGapMd - _back.layout.height, _body.layout.height, PixelSnapTolerance);

            // 同じ子画面で中身が縮んだら basis も中身の高さへ戻る（設定・バグ報告は子画面ごとに作るが、部品は縮みにも追う）
            // When the content shrinks in the same sub-page the basis follows it back down
            SetSectionHeight(80f);
            Assert.AreEqual(80f, _body.resolvedStyle.flexBasis.value, PixelSnapTolerance);
            Assert.AreEqual(80f, _body.layout.height, PixelSnapTolerance);
        }

        [Test]
        public void 追従を2回頼んでも本文のbasisは中身の高さのまま伸び縮みする()
        {
            // 同じメソッドの登録は UITK が 1 つにまとめるので、2 回目の呼び出しで basis の解き方は変わらない
            // UITK keeps one registration per method, so a second call leaves how the basis resolves unchanged
            _body.FollowContentHeightAsFlexBasis();
            SetSectionHeight(120f);
            Assert.AreEqual(120f, _body.resolvedStyle.flexBasis.value, PixelSnapTolerance);
            SetSectionHeight(60f);
            Assert.AreEqual(60f, _body.resolvedStyle.flexBasis.value, PixelSnapTolerance);
            Assert.AreEqual(60f, _body.layout.height, PixelSnapTolerance);
        }

        private void SetSectionHeight(float height)
        {
            // 寸法変化は GeometryChangedEvent→basis へ届く
            // A content size change reaches the basis through GeometryChangedEvent and applies on the next layout
            _section.style.height = height;
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }
    }
}
