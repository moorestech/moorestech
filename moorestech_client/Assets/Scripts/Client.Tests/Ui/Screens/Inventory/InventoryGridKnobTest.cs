using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Client.Tests.Ui.Theme.Calc;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Screens.Inventory
{
    /// <summary>
    /// 持ち物のグリッドの前計算のつまみを Web の calc で検算する（L2）
    /// Rechecks the inventory grid's precomputed knobs against the Web calc (L2)
    /// </summary>
    public class InventoryGridKnobTest
    {
        private static readonly Regex Declaration = new(@"^\s*(--[\w-]+)\s*:\s*([^;]+);(?:\s*/\*\s*(.*?)\s*\*/)?", RegexOptions.Multiline);

        [Test]
        public void 持ち物のつまみはトークンの積と一致する()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var declared = new Dictionary<string, string>();
            foreach (Match match in Declaration.Matches(File.ReadAllText(Path.Combine(root, "moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/Tokens.uss"))))
            {
                declared[match.Groups[1].Value] = match.Groups[2].Value.Trim();
            }

            // calc 注記の行を全て検算（3 行あること）
            // Recheck all calc-noted rows (require three)
            var evaluator = new CssCalcEvaluator(declared);
            var checkedRows = 0;
            foreach (Match match in Declaration.Matches(File.ReadAllText(Path.Combine(root, "moorestech_client/Assets/Scripts/Client.Ui/Screens/Inventory/Inventory.uss"))))
            {
                var comment = match.Groups[3].Value;
                if (!comment.StartsWith("calc: ")) continue;
                var value = float.Parse(match.Groups[2].Value.Trim().Replace("px", ""), CultureInfo.InvariantCulture);
                Assert.AreEqual(evaluator.Evaluate(comment.Substring(6).Split(';')[0]), value, 0.01f, match.Groups[1].Value);
                checkedRows++;
            }
            Assert.AreEqual(3, checkedRows);
        }
    }
}
