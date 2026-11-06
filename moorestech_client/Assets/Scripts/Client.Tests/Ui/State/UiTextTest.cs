using System;
using Client.Localization;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.State
{
    /// <summary>
    /// UiTextの種ごとの解決を確かめる
    /// Verifies UiText resolution per kind
    /// </summary>
    public class UiTextTest
    {
        [Test]
        public void 辞書キーの文は辞書から解く()
        {
            var text = new UiText(LocalizationKeys.Ui.PauseMenu.Settings);

            Assert.AreEqual(UiTextSource.Dictionary, text.Source);
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.PauseMenu.Settings), text.Resolve());
        }

        [Test]
        public void コンテンツ辞書キーの文はコンテンツの口で解く()
        {
            var key = ContentLocalizationKeys.ItemName(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"));
            var text = UiText.FromContent(key);

            Assert.AreEqual(UiTextSource.Content, text.Source);
            Assert.AreEqual(Localize.GetContent(key), text.Resolve());
        }

        [Test]
        public void 解決済みの文はそのまま返す()
        {
            var text = UiText.FromVerbatim("Blueprint 1");

            Assert.AreEqual(UiTextSource.Verbatim, text.Source);
            Assert.AreEqual("Blueprint 1", text.Resolve());
        }

        [Test]
        public void 解決済みの文にnullを渡すと作る時点で止める()
        {
            Assert.Throws<ArgumentNullException>(() => UiText.FromVerbatim(null));
        }

        [Test]
        public void 先頭に引数を足した写しを返す()
        {
            var original = new UiText(LocalizationKeys.Ui.Common.ItemFallback);
            var added = original.WithLeadingParam("itemId", "X");

            var expected = new UiText(LocalizationKeys.Ui.Common.ItemFallback, new[] { new LocalizationTextParam("itemId", "X") });
            Assert.AreEqual(expected.Resolve(), added.Resolve());
            Assert.AreNotEqual(original.Resolve(), added.Resolve());
        }

        [Test]
        public void 辞書キー以外に引数を足すと止める()
        {
            Assert.Throws<InvalidOperationException>(() => UiText.FromVerbatim("a").WithLeadingParam("itemId", "X"));
        }
    }
}
