using System.Text.RegularExpressions;
using Client.Localization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Localization.Resolution
{
    public class LocalizationTextInterpolatorTest
    {
        [Test]
        public void ValueContainingBracesIsNotReinterpolated()
        {
            // 差し込んだ値の中の{...}は再置換しない
            // Braces inside an inserted value are not interpolated again
            var text = LocalizationTextInterpolator.Interpolate("test.key", "{a}/{b}", new[]
            {
                new LocalizationTextParam("a", "{b}"),
                new LocalizationTextParam("b", "2"),
            });

            Assert.AreEqual("{b}/2", text);
        }

        [Test]
        public void DuplicateParamNameUsesFirstValueAndReportsError()
        {
            // 同名パラメータは先勝ちで埋め、重複をエラーログで知らせる
            // A duplicate name fills with the first value and is reported as an error
            LogAssert.Expect(LogType.Error, new Regex(@"test\.key.*'count'"));
            var text = LocalizationTextInterpolator.Interpolate("test.key", "x{count}", new[]
            {
                new LocalizationTextParam("count", "1"),
                new LocalizationTextParam("count", "2"),
            });

            Assert.AreEqual("x1", text);
        }
    }
}
