using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Ui.Foundation.Theme;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Components
{
    /// <summary>
    /// テーマのトークン USS 群をテキストで読み、var() をたどって最終の値を返す（部品 L1 の期待値用）
    /// Reads the theme token USS files as text and follows var() to the final value (expected values for component L1)
    /// </summary>
    public static class ThemeTokenFile
    {
        private const string ThemeDirectory = "Assets/Scripts/Client.Ui/Foundation/Theme";
        private const float ColorTolerance = 0.004f;
        private static readonly Regex VarReference = new(@"^var\((--[\w-]+)\)$");
        private static readonly Regex RgbFunction = new(@"rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*(?:,\s*([\d.]+)\s*)?\)");

        public static string Resolve(string tokenName)
        {
            // var(--x) だけなら参照先をたどる
            // A bare var(--x) value is followed to its target
            var value = ReadDeclaredValue(tokenName);
            var reference = VarReference.Match(value);
            return reference.Success ? Resolve(reference.Groups[1].Value) : value;

            #region Internal

            string ReadDeclaredValue(string tokenName)
            {
                // 宣言はテーマの USS のどれか 1 枚にある。名前の前に英数とハイフンが続かない所だけを拾う
                // The declaration lives in one of the theme USS files; only matches not preceded by a word char or hyphen count
                var pattern = new Regex(@"(?<![\w-])" + Regex.Escape(tokenName) + @"\s*:\s*([^;]+);");
                var directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), ThemeDirectory);
                foreach (var path in Directory.GetFiles(directory, "*.uss", SearchOption.AllDirectories).OrderBy(path => path))
                {
                    var match = pattern.Match(File.ReadAllText(path));
                    if (match.Success) return Regex.Replace(match.Groups[1].Value, @"/\*.*?\*/", "").Trim();
                }
                Assert.Fail($"{tokenName} is not declared in {ThemeDirectory}");
                return null;
            }

            #endregion
        }

        public static float ResolvePixels(string tokenName)
        {
            var value = Resolve(tokenName);
            Assert.IsTrue(UssTokenValue.TryParsePixels(value, out var pixels), $"{tokenName} = {value}");
            return pixels;
        }

        public static Color ResolveColor(string tokenName)
        {
            // #hex と名前の色は Unity の解釈に任せ、rgb()/rgba() は 0..255 と α で読む
            // Hex and named colors go through Unity's parser; rgb()/rgba() are read as 0..255 channels and alpha
            var value = Resolve(tokenName);
            if (ColorUtility.TryParseHtmlString(value, out var parsed)) return parsed;
            var match = RgbFunction.Match(value);
            Assert.IsTrue(match.Success, $"{tokenName} = {value}");
            var alpha = match.Groups[4].Success ? Channel(4) : 1f;
            return new Color(Channel(1) / 255f, Channel(2) / 255f, Channel(3) / 255f, alpha);

            #region Internal

            float Channel(int group)
            {
                return float.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
            }

            #endregion
        }

        public static void AssertSameColor(Color expected, Color actual, string what)
        {
            Assert.AreEqual(expected.r, actual.r, ColorTolerance, $"{what}.r");
            Assert.AreEqual(expected.g, actual.g, ColorTolerance, $"{what}.g");
            Assert.AreEqual(expected.b, actual.b, ColorTolerance, $"{what}.b");
            Assert.AreEqual(expected.a, actual.a, ColorTolerance, $"{what}.a");
        }
    }
}
