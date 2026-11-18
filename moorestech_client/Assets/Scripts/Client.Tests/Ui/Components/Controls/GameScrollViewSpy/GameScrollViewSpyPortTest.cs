using System.Text.RegularExpressions;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Controls.GameScrollViewSpy
{
    /// <summary>
    /// スクロールスパイの口を確かめる（L1）
    /// Verifies the scroll-spy port (L1)
    /// </summary>
    public class GameScrollViewSpyPortTest : UiTestHost
    {
        private GameScrollView _scrollView;
        private VisualElement _heading;
        private VisualElement _filler;

        [SetUp]
        public void MountScrollView()
        {
            _scrollView = new GameScrollView("spy-port-scroll", GameScrollAxis.Vertical);
            // 部品の USS は flex-grow: 1 なので、列のルートでは高さ 200 が親いっぱい（720）に伸びる。伸びを止めて 200 に固定する
            // The component USS has flex-grow: 1, which stretches height 200 to the parent (720) in a column root; stop the grow to pin 200
            _scrollView.style.flexGrow = 0;
            _scrollView.style.height = 200;
            var top = Block("spy-top", 300);
            _heading = Block("spy-heading", 40);
            _filler = Block("spy-filler", 600);
            _scrollView.Add(top);
            _scrollView.Add(_heading);
            _scrollView.Add(_filler);
            rootVisualElement.Add(_scrollView);
            simulate.FrameUpdate();
        }

        [Test]
        public void 見出しの位置はスクロールしても中身の上端からの距離のまま()
        {
            Assert.IsTrue(_scrollView.TryGetContentTop(_heading, out var before));
            _scrollView.SetScrollOffset(250f);
            simulate.FrameUpdate();
            Assert.IsTrue(_scrollView.TryGetContentTop(_heading, out var after));

            Assert.AreEqual(300f, before, 0.5f);
            Assert.AreEqual(300f, after, 0.5f);
            Assert.AreEqual(200f, _scrollView.GetViewportHeight(), 0.5f);
            Assert.AreEqual(940f, _scrollView.GetContentHeight(), 0.5f);
        }

        [Test]
        public void 中身の高さが変わると知らせスクロールだけでは知らせない()
        {
            var changes = 0;
            using var subscription = _scrollView.OnContentLayoutChanged.Subscribe(_ => changes++);
            _scrollView.SetScrollOffset(120f);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.AreEqual(0, changes, "scroll alone");

            _filler.style.height = 900;
            simulate.FrameUpdate();
            Assert.AreEqual(1, changes, "content height changed");
            Assert.AreEqual(1240f, _scrollView.GetContentHeight(), 0.5f);
        }

        [Test]
        public void 中身の外の要素は位置を返さずエラーを出す()
        {
            var stranger = Block("spy-stranger", 10);
            rootVisualElement.Add(stranger);
            simulate.FrameUpdate();
            LogAssert.Expect(LogType.Error, new Regex(@"^\[GameScrollView\] spy-port-scroll: spy-stranger is not inside the content"));
            Assert.IsFalse(_scrollView.TryGetContentTop(stranger, out _));
        }

        private static VisualElement Block(string blockName, float height)
        {
            var block = new VisualElement { name = blockName };
            block.style.height = height;
            block.style.flexShrink = 0;
            return block;
        }
    }
}
