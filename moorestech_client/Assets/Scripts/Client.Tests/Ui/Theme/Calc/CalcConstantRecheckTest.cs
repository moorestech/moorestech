// moorestech_client/Assets/Scripts/Client.Tests/Ui/Theme/Calc/CalcConstantRecheckTest.cs
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Theme.Calc
{
    /// <summary>
    /// calcトークンの検算（L2）
    /// Rechecks calc tokens (L2)
    /// </summary>
    public class CalcConstantRecheckTest
    {
        private const string ThemeDirectory = "moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme";
        private static readonly string[] TokenFiles = { "Tokens.uss", "SkitTokens.uss", "SlotTokens.uss", "DerivedTokens.uss" };
        private static readonly Regex Declaration = new(@"^\s*(--[\w-]+)\s*:\s*([^;]+);(?:\s*/\*\s*(.*?)\s*\*/)?", RegexOptions.Multiline);
        private const string CalcCommentPrefix = "calc: ";

        [Test]
        public void EveryCalcCommentReproducesItsDeclaredValue()
        {
            var declared = new Dictionary<string, string>();
            var calcRows = new List<(string name, string value, string expression)>();
            foreach (var fileName in TokenFiles)
            {
                foreach (Match match in Declaration.Matches(File.ReadAllText(Path.Combine(RepositoryRoot(), ThemeDirectory, fileName))))
                {
                    declared[match.Groups[1].Value] = match.Groups[2].Value.Trim();
                    var comment = match.Groups[3].Value;
                    // 他の注記が続くときは ; の前までが式
                    // When other notes follow, the expression ends before ';'
                    if (comment.StartsWith(CalcCommentPrefix)) calcRows.Add((match.Groups[1].Value, match.Groups[2].Value.Trim(), comment.Substring(CalcCommentPrefix.Length).Split(';')[0]));
                }
            }

            Assert.IsNotEmpty(calcRows, "no calc: comments found");
            var evaluator = new CssCalcEvaluator(declared);
            foreach (var (name, value, expression) in calcRows)
            {
                Assert.AreEqual(evaluator.Evaluate(expression), ParsePx(value), 0.01f, $"{name}: {value} != calc({expression})");
            }

            #region Internal

            float ParsePx(string value)
            {
                return float.Parse(value.Replace("px", ""), CultureInfo.InvariantCulture);
            }

            string RepositoryRoot()
            {
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            }

            #endregion
        }

        [Test]
        public void EvaluatorHandlesPrecedenceParenthesesVarsAndRem()
        {
            var evaluator = new CssCalcEvaluator(new Dictionary<string, string> { { "--a", "10px" }, { "--n", "3" } });
            Assert.AreEqual(38f, evaluator.Evaluate("var(--a) * var(--n) + 8px"), 0.001f);
            Assert.AreEqual(5f, evaluator.Evaluate("(var(--a) - 8px) * 5 / 2"), 0.001f);
            Assert.AreEqual(10f, evaluator.Evaluate("0.625rem"), 0.001f);
            Assert.Throws<KeyNotFoundException>(() => evaluator.Evaluate("var(--missing) + 1px"));
        }
    }
}
