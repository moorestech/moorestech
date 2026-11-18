using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Conventions
{
    /// <summary>
    /// 本番の UITK の文は WrapSafeLabel（か派生）で作り、素の Label・TextElement を作らないことを検査（L2）
    /// Checks production UITK text is built from WrapSafeLabel (or a subclass), never a plain Label or TextElement (L2)
    /// </summary>
    /// <remarks>
    /// 素の Label は 2 字分より狭い幅で「字＋開き括弧＋字」を折り返すと UITK TextCore ごと固まる（bd moorestech-fcpzz・設計書 A-224）。測り役は折り返して測る幅を Trimmer が必要な幅以上に限るので除く
    /// A plain Label freezes UITK TextCore when it wraps "glyph + opening bracket + glyph" narrower than two glyphs (bd moorestech-fcpzz, design A-224); the measurer is exempt because the trimmer only measures at a safe width
    /// </remarks>
    public class WrapSafeLabelConventionTest
    {
        private const string ClientUiDirectory = "moorestech_client/Assets/Scripts/Client.Ui";
        private static readonly string[] AllowedFiles =
        {
            "Foundation/Text/WrapSafe/WrapSafeLabel.cs",
            "Foundation/Text/LineEndPunctuationMeasurer.cs",
        };
        // 素の Label・TextElement の生成と継承（名前空間つき・行末で改行・行末のコメントも）、自分で文を描く Button・Toggle・Foldout へ文を入れる生成
        // Plain Label/TextElement construction and inheritance (qualified, broken at the line end or followed by a comment), and Button/Toggle/Foldout constructed with text they draw themselves
        private static readonly Regex PlainTextElement = new(
            @"\bnew\s+(UnityEngine\.UIElements\.)?(Label|TextElement)\s*([\(\{]|$)" +
            @"|:\s*(UnityEngine\.UIElements\.)?(Label|TextElement)\s*(\{|,|where\b|//|$)" +
            @"|\b(Label|TextElement)\s+\w+\s*=\s*new\s*\(" +
            @"|\bnew\s+(UnityEngine\.UIElements\.)?(Button|Toggle|Foldout)\s*(\(\s*""|(\(\s*\))?\s*\{[^}]*\b(text|label)\s*=)");

        [Test]
        public void ClientUiCreatesNoPlainLabelOrTextElement()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ClientUiDirectory));
            var violations = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .Where(relative => !AllowedFiles.Contains(relative))
                .SelectMany(relative => FindViolations(relative, File.ReadAllText(Path.Combine(root, relative))))
                .ToList();
            Assert.IsEmpty(violations, string.Join("\n", violations));
        }

        [Test]
        public void RuleCatchesConstructionAndInheritanceButNotWrapSafeLabel()
        {
            Assert.AreEqual(1, FindViolations("A.cs", "var label = new Label { name = \"x\" };").Count);
            Assert.AreEqual(1, FindViolations("A.cs", "var label = new Label(text);").Count);
            Assert.AreEqual(1, FindViolations("A.cs", "public sealed class KeyCap : Label").Count);
            Assert.AreEqual(1, FindViolations("A.cs", "var t = new TextElement();").Count);
            Assert.AreEqual(1, FindViolations("A.cs", "private readonly Label _measurer = new();").Count);
            Assert.AreEqual(1, FindViolations("A.cs", "var label = new Label").Count);
            Assert.AreEqual(1, FindViolations("A.cs", "var label = new UnityEngine.UIElements.Label(text);").Count);
            Assert.AreEqual(1, FindViolations("A.cs", "public sealed class KeyCap : Label // key").Count);
            Assert.AreEqual(1, FindViolations("A.cs", "var button = new Button { text = \"OK\" };").Count);
            Assert.AreEqual(1, FindViolations("A.cs", "var toggle = new Toggle(\"Sound\");").Count);
            Assert.AreEqual(1, FindViolations("A.cs", "var foldout = new Foldout() { text = title };").Count);
            Assert.IsEmpty(FindViolations("A.cs", "var button = new Button(OnClick) { name = \"x\" };"));
            Assert.IsEmpty(FindViolations("A.cs", "var label = new LabelSet();"));
            Assert.IsEmpty(FindViolations("A.cs", "var label = new WrapSafeLabel { name = \"x\" };"));
            Assert.IsEmpty(FindViolations("A.cs", "public sealed class KeyCap : WrapSafeLabel"));
            Assert.IsEmpty(FindViolations("A.cs", "// a comment about new Label { }"));
            Assert.IsEmpty(FindViolations("A.cs", "label.Q<Label>(); Label found = null;"));
        }

        private static System.Collections.Generic.List<string> FindViolations(string relativePath, string source)
        {
            return source.Split('\n')
                .Select((line, index) => (line: line.TrimEnd('\r'), number: index + 1))
                .Where(entry => !entry.line.TrimStart().StartsWith("//") && PlainTextElement.IsMatch(entry.line))
                .Select(entry => $"{relativePath}:{entry.number}: build text with WrapSafeLabel (bd moorestech-fcpzz): {entry.line.Trim()}")
                .ToList();
        }
    }
}
