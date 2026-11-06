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
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.PauseMenu.Settings), text.Resolve());
        }

        [Test]
        public void コンテンツ辞書キーの文はコンテンツの口で解く()
        {
            var key = ContentLocalizationKeys.ItemName(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"));
            var text = UiText.FromContent(key);
            Assert.AreEqual(Localize.GetContent(key), text.Resolve());
        }

        [Test]
        public void 解決済みの文はそのまま返す()
        {
            var text = UiText.FromVerbatim("Blueprint 1");
            Assert.AreEqual("Blueprint 1", text.Resolve());
        }

        [Test]
        public void 解決済みの文にnullを渡すと作る時点で止める()
        {
            Assert.Throws<ArgumentNullException>(() => UiText.FromVerbatim(null));
        }
    }
}
