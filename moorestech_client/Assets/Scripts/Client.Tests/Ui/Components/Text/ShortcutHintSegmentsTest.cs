// moorestech_client/Assets/Scripts/Client.Tests/Ui/Components/Text/ShortcutHintSegmentsTest.cs
using Client.Ui.Components.Text;
using NUnit.Framework;

namespace Client.Tests.Ui.Components.Text
{
    /// <summary>
    /// kbd 前後の割り規則の確認
    /// Checks the before/after kbd split rule
    /// </summary>
    public class ShortcutHintSegmentsTest
    {
        private const string M = ShortcutHintSegments.Marker;

        [Test]
        public void prefixは文に印があっても割らずkbdを先頭にする()
        {
            var segments = ShortcutHintSegments.Split($"Press {M} to open", ShortcutHintLayout.Prefix);
            Assert.AreEqual("", segments.Before);
            Assert.AreEqual($"Press {M} to open", segments.After);
            Assert.AreEqual(ShortcutHintSplitKind.KeyFirst, segments.Kind);
        }

        [Test]
        public void inlineは印の位置で前後に割る()
        {
            var segments = ShortcutHintSegments.Split($"{M}キーで開く", ShortcutHintLayout.Inline);
            Assert.AreEqual("", segments.Before);
            Assert.AreEqual("キーで開く", segments.After);

            segments = ShortcutHintSegments.Split($"Press {M} to open", ShortcutHintLayout.Inline);
            Assert.AreEqual("Press ", segments.Before);
            Assert.AreEqual(" to open", segments.After);
            Assert.AreEqual(ShortcutHintSplitKind.Inline, segments.Kind);
        }

        [Test]
        public void inlineで印が無ければprefixへ退避し欠けを知らせる()
        {
            var segments = ShortcutHintSegments.Split("Open", ShortcutHintLayout.Inline);
            Assert.AreEqual("", segments.Before);
            Assert.AreEqual("Open", segments.After);
            Assert.AreEqual(ShortcutHintSplitKind.KeyFirstBecauseMarkerMissing, segments.Kind);
        }
    }
}
