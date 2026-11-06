using System.Linq;
using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Panels;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Panels
{
    /// <summary>
    /// GamePanel（default）の構造・題・副次アクション・本文の寸法を本番テーマのパネルで確かめる（L1）
    /// Verifies GamePanel (default) structure, title, title action and body size on a panel with the production theme (L1)
    /// </summary>
    public class GamePanelTest : UiTestHost
    {
        // Web の .panel の padding: 8px 10px 10px 28px の左右
        // Left and right of the Web .panel padding: 8px 10px 10px 28px
        private const float WebHorizontalPadding = 28f + 10f;

        private GamePanel _panel;

        [SetUp]
        public void AddPanel()
        {
            // 題の文を解くため辞書を用意する（単独実行でも未初期化にしない）
            // Prepare the dictionary to resolve titles, even when the test runs alone
            Localize.Initialize();
            _panel = new GamePanel("test-game-panel", GamePanelFrame.Default);
            _panel.style.width = 400;
            _panel.style.height = 300;
            rootVisualElement.Add(_panel);
        }

        [Test]
        public void 名前はtestidで本文に足した子は本文の箱に入る()
        {
            var child = new Label("body");
            _panel.Add(child);
            Assert.AreEqual("test-game-panel", _panel.name);
            // parent は contentContainer の持ち主を返すので、物理の親を見る
            // parent returns the contentContainer owner, so check the physical parent
            Assert.IsTrue(child.hierarchy.parent.ClassListContains("game-panel__body"));
        }

        [Test]
        public void SetTitleまでは題の行と罫線を隠し付けると出す()
        {
            simulate.FrameUpdate();
            Assert.IsTrue(TitleRowElements().All(element => element.resolvedStyle.display == DisplayStyle.None));

            _panel.SetTitle(new UiText(LocalizationKeys.Ui.PauseMenu.Title));
            simulate.FrameUpdate();
            Assert.IsTrue(_panel.ClassListContains("game-panel--titled"));
            Assert.IsTrue(TitleRowElements().All(element => element.resolvedStyle.display == DisplayStyle.Flex));
        }

        [Test]
        public void 題は渡したUiTextの文を出し2回目は差し替える()
        {
            _panel.SetTitle(new UiText(LocalizationKeys.Ui.PauseMenu.Title));
            _panel.SetTitle(new UiText(LocalizationKeys.Ui.BlockInventory.TrainInventory));
            simulate.FrameUpdate();
            var titles = _panel.Query<Label>(className: "game-panel__title").ToList();
            Assert.AreEqual(1, titles.Count);
            Assert.AreEqual(new UiText(LocalizationKeys.Ui.BlockInventory.TrainInventory).Resolve(), titles[0].text);
        }

        [Test]
        public void 副次アクションは題の行の右端で上下中央に寄る()
        {
            var action = SizedAction("test-title-action");
            _panel.SetTitle(new UiText(LocalizationKeys.Ui.BlockInventory.TrainInventory));
            _panel.SetTitleAction(action);
            simulate.FrameUpdate();
            var header = _panel.Q(className: "game-panel__header");
            Assert.AreEqual(header.worldBound.xMax, action.worldBound.xMax, 1f);
            Assert.AreEqual(header.worldBound.center.y, action.worldBound.center.y, 1f);
        }

        [Test]
        public void 副次アクションを2回渡すと前の要素を外す()
        {
            var first = SizedAction("test-title-action-1");
            var second = SizedAction("test-title-action-2");
            _panel.SetTitleAction(first);
            _panel.SetTitleAction(second);
            Assert.IsNull(first.parent);
            Assert.IsTrue(second.parent.ClassListContains("game-panel__title-action"));
        }

        [Test]
        public void 面と三角と罫線は拾わない()
        {
            foreach (var className in new[] { "game-panel__face", "game-panel__bottom-deco", "game-panel__deco-line--top", "game-panel__deco-line--bottom" })
            {
                Assert.AreEqual(PickingMode.Ignore, _panel.Q(className: className).pickingMode, className);
            }
        }

        [Test]
        public void 本文の幅はパネル幅からWebの左右paddingを引いた値()
        {
            _panel.SetTitle(new UiText(LocalizationKeys.Ui.PauseMenu.Title));
            _panel.Add(new Label("line 1"));
            simulate.FrameUpdate();
            var body = _panel.Q(className: "game-panel__body");
            Assert.AreEqual(_panel.layout.width - WebHorizontalPadding, body.layout.width, 1f);
            Assert.Greater(body.layout.height, 0f);
        }

        [Test]
        public void 描画でエラーを出さない()
        {
            _panel.SetTitle(new UiText(LocalizationKeys.Ui.PauseMenu.Title));
            _panel.Add(new Label("line 1"));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();
        }

        private VisualElement[] TitleRowElements()
        {
            return new[]
            {
                _panel.Q(className: "game-panel__deco-line--top"),
                _panel.Q(className: "game-panel__header"),
                _panel.Q(className: "game-panel__deco-line--bottom"),
            };
        }

        private static VisualElement SizedAction(string testid)
        {
            var action = new VisualElement { name = testid };
            action.style.width = 40;
            action.style.height = 12;
            return action;
        }
    }
}
