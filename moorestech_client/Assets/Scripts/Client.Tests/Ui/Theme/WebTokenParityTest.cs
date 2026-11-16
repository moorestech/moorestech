using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Theme
{
    /// <summary>
    /// UITK のトークン USS に USS が解釈しない CSS 構文が残っていないことを確かめる（L2）
    /// Verifies the UITK token USS holds no CSS syntax that USS cannot parse (L2)
    /// </summary>
    public class WebTokenParityTest
    {
        private const string UssTokenDirectory = "moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme";
        private static readonly string[] UssTokenFiles = { "Tokens.uss", "SkitTokens.uss", "SlotTokens.uss" };

        [Test]
        public void UssTokensContainNoUnconvertedCssSyntax()
        {
            // USS が解釈しない CSS 構文が変換後に残っていない
            // No CSS syntax that USS cannot parse remains after conversion
            var forbidden = new[] { "calc(", "rem", "color-mix", "rgb(from", "em;", "linear-gradient", "solid" };
            foreach (var fileName in UssTokenFiles)
            {
                var text = File.ReadAllText(Path.Combine(RepositoryRoot(), UssTokenDirectory, fileName));
                foreach (var syntax in forbidden)
                {
                    Assert.IsFalse(text.Contains(syntax), $"{fileName} still contains '{syntax}'");
                }
            }
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        }
    }
}
