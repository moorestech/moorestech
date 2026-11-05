using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Conventions
{
    /// <summary>
    /// Client.Ui 配下の全 USS・UXML が GameUi の規約を守ること、検査関数が各規約の違反を落とすことを確かめる（L2）
    /// Verifies that every USS/UXML under Client.Ui follows the GameUi conventions and that the checker catches each rule's violation (L2)
    /// </summary>
    public class UiStyleConventionTest
    {
        private static string ClientUiDirectory => Path.Combine(Application.dataPath, "Scripts", "Client.Ui");

        [Test]
        public void AllClientUiUssFollowConventions()
        {
            var files = Directory.GetFiles(ClientUiDirectory, "*.uss", SearchOption.AllDirectories);

            // 0 件なら探す場所を間違えているので、空振りで緑にしない
            // Zero files means the search looked in the wrong place, so do not pass vacuously
            Assert.IsNotEmpty(files, $"no USS found under {ClientUiDirectory}");
            var report = new List<string>();
            foreach (var path in files)
            {
                var allowColorLiterals = UiStyleConventionRules.ColorLiteralExemptUssFileNames.Contains(Path.GetFileName(path));
                report.AddRange(UiStyleConventionRules.CheckUss(File.ReadAllText(path), allowColorLiterals).Select(v => $"{path}: {v}"));
            }
            Assert.IsEmpty(report, string.Join(Environment.NewLine, report));
        }

        [Test]
        public void AllClientUiUxmlFollowConventions()
        {
            // UXML はまだ 0 枚でもよい（段階 1 の input レーン時点）
            // UXML may still be zero files (as of the stage 1 input lane)
            var report = new List<string>();
            foreach (var path in Directory.GetFiles(ClientUiDirectory, "*.uxml", SearchOption.AllDirectories))
            {
                report.AddRange(UiStyleConventionRules.CheckUxml(File.ReadAllText(path)).Select(v => $"{path}: {v}"));
            }
            Assert.IsEmpty(report, string.Join(Environment.NewLine, report));
        }

        [TestCase(".a {\n    color: #ff0000;\n}", UiStyleRule.ColorLiteral, 2)]
        [TestCase(".a { background-color: rgba(0, 0, 0, 0.5); }", UiStyleRule.ColorLiteral, 1)]
        [TestCase(".a { border-color: rgb(1, 2, 3); }", UiStyleRule.ColorLiteral, 1)]
        [TestCase(".list > .item:first-child { margin-left: 0; }", UiStyleRule.StructuralPseudoClass, 1)]
        [TestCase(".item:last-child { margin-right: 0; }", UiStyleRule.StructuralPseudoClass, 1)]
        [TestCase(".item:nth-child(2) { margin-top: 0; }", UiStyleRule.StructuralPseudoClass, 1)]
        [TestCase(".a { z-index: 3; }", UiStyleRule.ZIndex, 1)]
        [TestCase(".a { background-image: url(\"Icons/slot.png\"); }", UiStyleRule.BitmapReference, 1)]
        [TestCase(".a { background-image: url(\"Art/frame.psd\"); }", UiStyleRule.BitmapReference, 1)]
        [TestCase(".a { -unity-font-style: bold; }", UiStyleRule.BoldFontStyle, 1)]
        [TestCase(".a { -unity-font-style: bold-and-italic; }", UiStyleRule.BoldFontStyle, 1)]
        public void UssViolationIsCaught(string uss, UiStyleRule rule, int line)
        {
            var violations = UiStyleConventionRules.CheckUss(uss, false);
            Assert.IsTrue(violations.Any(v => v.Rule == rule && v.Line == line), $"{rule} at line {line} not caught: [{string.Join(", ", violations)}]");
        }

        [TestCase("<ui:UXML>\n<ui:Label text=\"Hello\" />\n</ui:UXML>", UiStyleRule.UxmlTextLiteral, 2)]
        [TestCase("<ui:UXML>\n<ui:VisualElement name=\"slot\" />\n<ui:VisualElement name=\"slot\" />\n</ui:UXML>", UiStyleRule.DuplicateName, 3)]
        [TestCase("<ui:UXML><ui:VisualElement style=\"color: #fff;\" /></ui:UXML>", UiStyleRule.ColorLiteral, 1)]
        [TestCase("<ui:UXML><ui:VisualElement style=\"-unity-font-style: bold;\" /></ui:UXML>", UiStyleRule.BoldFontStyle, 1)]
        [TestCase("<ui:UXML><ui:Image src=\"icon.jpg\" /></ui:UXML>", UiStyleRule.BitmapReference, 1)]
        public void UxmlViolationIsCaught(string uxml, UiStyleRule rule, int line)
        {
            var violations = UiStyleConventionRules.CheckUxml(uxml);
            Assert.IsTrue(violations.Any(v => v.Rule == rule && v.Line == line), $"{rule} at line {line} not caught: [{string.Join(", ", violations)}]");
        }

        [Test]
        public void ConformingTextHasNoViolation()
        {
            // ID セレクタの #add-button・変数参照・コメント内の色・空の text・別の name は違反でない
            // An #add-button id selector, variable references, colors inside comments, empty text and distinct names are not violations
            const string uss = "/* was #ff0000 */\n#add-button { color: var(--surface-navy); }\n.row:hover { opacity: 0.5; }";
            const string uxml = "<ui:UXML><!-- <ui:Label text=\"old\" /> --><ui:Label name=\"a\" text=\"\" /><ui:Label name=\"b\" /></ui:UXML>";
            Assert.IsEmpty(UiStyleConventionRules.CheckUss(uss, false));
            Assert.IsEmpty(UiStyleConventionRules.CheckUxml(uxml));
        }

        [Test]
        public void TokenUssMayHoldColorLiteralsButNotOtherViolations()
        {
            // トークン USS の例外は色リテラルだけ
            // The token USS exemption covers color literals only
            const string tokens = ":root {\n    --surface-navy: rgba(10, 14, 27, 0.8);\n    --accent: #ffbb24;\n}";
            Assert.IsEmpty(UiStyleConventionRules.CheckUss(tokens, true));
            Assert.AreEqual(2, UiStyleConventionRules.CheckUss(tokens, false).Count(v => v.Rule == UiStyleRule.ColorLiteral));
            Assert.IsTrue(UiStyleConventionRules.CheckUss(":root { z-index: 1; }", true).Any(v => v.Rule == UiStyleRule.ZIndex));
        }
    }
}
