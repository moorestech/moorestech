using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Screens.Tutorial.Design
{
    /// <summary>
    /// Screens/Tutorial 配下の USS・.cs を文字列で読む（見た目の規則の静的検査用）
    /// Reads the USS and .cs files under Screens/Tutorial as text (for static checks of the visual rules)
    /// </summary>
    public static class TutorialDesignSource
    {
        private const string ScreenDirectory = "/Scripts/Client.Ui/Screens/Tutorial/";
        private static readonly Regex BlockComment = new(@"/\*.*?\*/", RegexOptions.Singleline);
        private static readonly Regex LineComment = new(@"(?m)//.*$");

        public static readonly Regex ColorLiteral = new(@"#[0-9a-fA-F]{3,8}\b|rgba?\(");
        public static readonly Regex MillisecondLiteral = new(@"\b\d+(\.\d+)?m?s\b");

        public static string ReadScreenFile(string relativePath)
        {
            // コメントには Web の CSS の数字が出るので、外した本文を返す
            // Comments quote Web CSS numbers, so return the body with comments removed
            var text = File.ReadAllText(Application.dataPath + ScreenDirectory + relativePath);
            return LineComment.Replace(BlockComment.Replace(text, ""), "");
        }

        public static string[] ListScreenFiles(string relativeDirectory, string searchPattern)
        {
            var root = Application.dataPath + ScreenDirectory;
            var files = Directory.GetFiles(root + relativeDirectory, searchPattern, SearchOption.AllDirectories);

            // Windows の区切り \ を / へ揃え、どの OS でも同じ相対パスで比べる
            // Normalise Windows \ separators to / so relative paths compare the same on every OS
            return files.Select(path => path.Substring(root.Length).Replace('\\', '/')).OrderBy(path => path).ToArray();
        }

        public static string Rule(string uss, string selector)
        {
            // 行頭のセレクタ 1 つだけのブロックの中身を返す
            // Returns the body of the block whose single selector starts the line
            var match = Regex.Match(uss, @"(?m)^" + Regex.Escape(selector) + @"\s*\{([^}]*)\}");
            Assert.IsTrue(match.Success, $"{selector} block not found");
            return match.Groups[1].Value;
        }

        public static string Squash(string text)
        {
            return Regex.Replace(text, @"\s+", " ");
        }
    }
}
