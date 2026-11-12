using System.Linq;
using System.Text.RegularExpressions;
using Client.Game.InGame.UI.Tooltip;
using Client.Localization;
using Client.WebUiHost.Game.Topics;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Tooltip
{
    /// <summary>
    ///     同名パラメータを持つ行でも ui.tooltip の配信が止まらないことを検証するテスト
    ///     Tests that a line with duplicate parameter names does not stop ui.tooltip delivery
    /// </summary>
    public class TooltipTopicDuplicateParamTest
    {
        [Test]
        public void 同名パラメータは先勝ちで辞書化しエラーログを出す()
        {
            var presentation = new TooltipPresentation(new[] { new TooltipLine(
                LocalizationKeys.Ui.Tooltip.RequiredItems, new[] { new LocalizationTextParam("items", "Iron"), new LocalizationTextParam("items", "Copper") }) });

            LogAssert.Expect(LogType.Error, new Regex("items"));
            var dto = TooltipTopic.ToDto(presentation);

            Assert.AreEqual("Iron", dto.Lines[0].TextParams["items"]);
        }

        [Test]
        public void 同名パラメータはWebへ送る値とCSharpの補間で同じ値を採る()
        {
            Localize.Initialize();
            var line = new TooltipLine(LocalizationKeys.Ui.Tooltip.RequiredItems, new[] { new LocalizationTextParam("items", "Iron"), new LocalizationTextParam("items", "Copper") });

            // Web は DTO の辞書で枠を埋め、C# は行の並びのまま補間器で埋める
            // The web fills slots from the DTO dictionary; C# fills them from the line's own list via the interpolator
            LogAssert.Expect(LogType.Error, new Regex("items"));
            var dto = TooltipTopic.ToDto(new TooltipPresentation(new[] { line }));
            var webText = Localize.GetFormatted(line.Key, dto.Lines[0].TextParams.Select(param => new LocalizationTextParam(param.Key, param.Value)).ToArray());
            LogAssert.Expect(LogType.Error, new Regex("items"));
            var csharpText = Localize.GetFormatted(line.Key, line.TextParams);

            Assert.AreEqual(csharpText, webText, "Web と C# で同名パラメータの勝ち方が食い違い、描く側によって表示が変わる");
        }
    }
}
