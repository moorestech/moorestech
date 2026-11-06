using System.IO;
using System.Text.RegularExpressions;
using Client.Ui.State.CursorTooltip;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.CursorTooltip
{
    /// <summary>
    /// ポインタからの余白が Web の tooltipPosition.ts の OFFSET と同じ値であることを確かめる（L0。片側だけ変えると Web と位置がずれる）
    /// Verifies the pointer offset equals OFFSET in Web tooltipPosition.ts (L0; changing one side alone shifts the position from Web)
    /// </summary>
    public class CursorTooltipOffsetWebParityTest
    {
        private const string TooltipPositionPath = "moorestech_web/webui/src/shared/tooltip/tooltipPosition.ts";

        [Test]
        public void 余白はWebのOFFSETと同じ()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var text = File.ReadAllText(Path.Combine(root, TooltipPositionPath));

            // `const OFFSET = 12;` の数値を拾う
            // Pick the number from `const OFFSET = 12;`
            var match = Regex.Match(text, @"const\s+OFFSET\s*=\s*(\d+(?:\.\d+)?)");
            Assert.IsTrue(match.Success, $"OFFSET not found in {TooltipPositionPath}");
            Assert.AreEqual(float.Parse(match.Groups[1].Value), CursorTooltipPlacement.Offset);
        }
    }
}
