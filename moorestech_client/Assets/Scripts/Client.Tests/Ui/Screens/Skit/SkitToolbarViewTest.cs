using Client.Ui.Components.Controls;
using Client.Ui.Screens.Skit.Parts.Toolbar;
using NUnit.Framework;
using static Client.Tests.Ui.Components.ComponentStyleAssert;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Skit
{
    /// <summary>
    /// ツールバーの 4 つのボタンが session と版を付けて操作を送り、許可とスキップ中で押せなくなることを確かめる（L1。Web SkitToolbar.tsx）
    /// Verifies that the four toolbar buttons send operations with session and revision, and become unpressable without allowance or while skipping (L1; Web SkitToolbar.tsx)
    /// </summary>
    public class SkitToolbarViewTest : ScreenTestHost
    {
        private RecordingSkitCommands _commands;
        private SkitToolbar _toolbar;

        [SetUp]
        public void MountToolbar()
        {
            _commands = new RecordingSkitCommands();
            _toolbar = Mount(new SkitToolbar(_commands, Toaster));
        }

        [Test]
        public void ツールは版を付けて自動スキップUI非表示を送り自動ONはシアンの印を持つ()
        {
            _toolbar.Render(SkitTestData.Controls(4, true, false, true, false, false, SkitTestData.TextIntents));
            simulate.FrameUpdate();
            Assert.IsTrue(Find<IconButton>(_toolbar, "skit-tool-auto").ClassListContains(SkitToolbar.ActiveToolUssClassName));
            Assert.IsFalse(IsShown(Find<IconButton>(_toolbar, "skit-show-ui")));

            // 画面 USS の上書きが IconButton の既定（--text-muted）に勝つ: 自動 ON はシアン、他は高コントラスト
            // The screen USS override beats IconButton's default (--text-muted): auto-on is cyan, the rest high contrast
            AssertColor(Rgba(0, 221, 255, 1f), GlyphColor("skit-tool-auto"), "--select-cyan");
            AssertColor(Hex("#FFFFFF"), GlyphColor("skit-tool-skip"), "--text-high-contrast");

            simulate.Click(Find<IconButton>(_toolbar, "skit-tool-auto"));
            simulate.Click(Find<IconButton>(_toolbar, "skit-tool-skip"));
            simulate.Click(Find<IconButton>(_toolbar, "skit-tool-hide-ui"));

            Assert.AreEqual(3, _commands.Issued.Count);
            Assert.AreEqual(new SkitCommandRecord(SkitCommandKind.SetAuto, SkitTestData.SessionId, 4, null, false), _commands.Issued[0], "自動 ON から押すと OFF を送る");
            Assert.AreEqual(new SkitCommandRecord(SkitCommandKind.Skip, SkitTestData.SessionId, 4, null, false), _commands.Issued[1]);
            Assert.AreEqual(new SkitCommandRecord(SkitCommandKind.SetUiHidden, SkitTestData.SessionId, 4, null, true), _commands.Issued[2]);
        }

        [Test]
        public void 許可の無い操作とスキップ中のスキップは押せない()
        {
            _toolbar.Render(SkitTestData.Controls(5, true, false, false, true, false, new[] { "skip" }));
            simulate.FrameUpdate();
            Assert.IsFalse(Find<IconButton>(_toolbar, "skit-tool-auto").enabledSelf);
            Assert.IsFalse(Find<IconButton>(_toolbar, "skit-tool-skip").enabledSelf, "スキップ中のスキップ");
            Assert.IsFalse(Find<IconButton>(_toolbar, "skit-tool-hide-ui").enabledSelf);
            AssertColor(Hex("#A6A7AB"), GlyphColor("skit-tool-auto"), "無効は --text-muted");

            simulate.Click(Find<IconButton>(_toolbar, "skit-tool-auto"));
            simulate.Click(Find<IconButton>(_toolbar, "skit-tool-skip"));
            Assert.AreEqual(0, _commands.Issued.Count);
        }

        [Test]
        public void UI非表示の間は復帰だけを出し押すと表示へ戻す()
        {
            _toolbar.Render(SkitTestData.Controls(6, true, false, false, false, true, SkitTestData.TextIntents));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(Find<IconButton>(_toolbar, "skit-tool-auto")));
            Assert.IsFalse(IsShown(Find<IconButton>(_toolbar, "skit-tool-skip")));
            Assert.IsFalse(IsShown(Find<IconButton>(_toolbar, "skit-tool-hide-ui")));
            Assert.IsTrue(IsShown(Find<IconButton>(_toolbar, "skit-show-ui")));

            simulate.Click(Find<IconButton>(_toolbar, "skit-show-ui"));
            Assert.AreEqual(new SkitCommandRecord(SkitCommandKind.SetUiHidden, SkitTestData.SessionId, 6, null, false), _commands.Issued[0]);
        }

        private Color GlyphColor(string testid)
        {
            var glyph = Find<IconButton>(_toolbar, testid).Q<SkitToolGlyph>();
            Assert.IsTrue(glyph.customStyle.TryGetValue(new CustomStyleProperty<Color>(IconButton.GlyphColorProperty), out var color), testid);
            return color;
        }
    }
}
