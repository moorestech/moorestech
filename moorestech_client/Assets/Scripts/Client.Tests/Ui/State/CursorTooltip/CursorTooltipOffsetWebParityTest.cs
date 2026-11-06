using System.IO;
using System.Text.RegularExpressions;
using Client.Ui.State.CursorTooltip;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.CursorTooltip
{
    /// <summary>
    /// 余白が Web の OFFSET と同値であること（L0）
    /// Offset equals Web OFFSET (L0)
    /// </summary>
    public class CursorTooltipOffsetWebParityTest
    {
        private const string TooltipPositionPath = "moorestech_web/webui/src/shared/tooltip/tooltipPosition.ts";

        [Test]
        public void 余白はWebのOFFSETと同じ()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var text = File.ReadAllText(Path.Combine(root, TooltipPositionPath));

            // OFFSET の数値を拾う
            // Pick the OFFSET number
            var match = Regex.Match(text, @"const\s+OFFSET\s*=\s*(\d+(?:\.\d+)?)");
            Assert.IsTrue(match.Success, $"OFFSET not found in {TooltipPositionPath}");
            Assert.AreEqual(float.Parse(match.Groups[1].Value), CursorTooltipPlacement.Offset);
        }
    }
}
