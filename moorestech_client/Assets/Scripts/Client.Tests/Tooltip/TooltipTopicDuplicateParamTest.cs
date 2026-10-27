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
        public void 同名パラメータは後勝ちで辞書化しエラーログを出す()
        {
            var presentation = new TooltipPresentation(new[] { new TooltipLine(
                LocalizationKeys.Ui.Tooltip.RequiredItems, new[] { new LocalizationTextParam("items", "Iron"), new LocalizationTextParam("items", "Copper") }) });

            LogAssert.Expect(LogType.Error, new Regex("items"));
            var dto = TooltipTopic.ToDto(presentation);

            Assert.AreEqual("Copper", dto.Lines[0].TextParams["items"]);
        }
    }
}
