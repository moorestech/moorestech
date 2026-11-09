using System;
using System.Text.RegularExpressions;
using Client.Localization;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
            // 足す前の文は引数が無く枠が残るので、補間器のエラーログを期待する
            // The text before adding has no params and keeps its slot, so expect the interpolator's error log
            LogAssert.Expect(LogType.Error, new Regex(@"no value for placeholder \{itemId\}"));
            Assert.AreNotEqual(original.Resolve(), added.Resolve());
        }

        [Test]
        public void 同名の引数は置き換えて重複させない()
        {
            var original = new UiText(LocalizationKeys.Ui.Common.ItemFallback, new[] { new LocalizationTextParam("itemId", "A") });
            var added = original.WithLeadingParam("itemId", "X");

            Assert.AreEqual(1, added.Params.Count);
            Assert.AreEqual(new LocalizationTextParam("itemId", "X"), added.Params[0]);
        }

        [Test]
        public void 辞書キー以外に引数を足すと止める()
        {
            Assert.Throws<InvalidOperationException>(() => UiText.FromVerbatim("a").WithLeadingParam("itemId", "X"));
        }

        [Test]
        public void 引数の無い辞書の文に枠が残ればエラーログを出す()
        {
            // {itemId} を持つ文を引数なしで解くと、枠は残り開発者が読めるエラーログが出る
            // Resolving a text with {itemId} and no params keeps the slot and logs an error a developer can read
            var text = new UiText(LocalizationKeys.Ui.Common.ItemFallback);

            LogAssert.Expect(LogType.Error, new Regex(@"no value for placeholder \{itemId\}"));
            StringAssert.Contains("{itemId}", text.Resolve());
        }
    }
}
