// moorestech_client/Assets/Scripts/Client.Tests/Ui/Conventions/UiThemeImportBundleTest.cs
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Conventions
{
    /// <summary>
    /// 束ねと重複を確かめる（L2）
    /// Verifies bundling and collisions (L2)
    /// </summary>
    public class UiThemeImportBundleTest
    {
        private const string TssPath = "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss";
        private const string ClientUiDirectory = "moorestech_client/Assets/Scripts/Client.Ui";

        [Test]
        public void ProductionThemeBundlesEveryClientUiUssOnceInSectionOrder()
        {
            var violations = UiThemeImportBundleRules.CheckBundle(File.ReadAllText(Path.Combine(RepositoryRoot(), TssPath)), ClientUiUss().Keys.ToList());
            Assert.IsEmpty(violations, string.Join("\n", violations));
        }

        [Test]
        public void ComponentAndScreenRootClassesAreUnique()
        {
            var violations = UiThemeImportBundleRules.CheckUniqueRootClasses(ClientUiUss());
            Assert.IsEmpty(violations, string.Join("\n", violations));
        }

        [Test]
        public void RulesReportMissingDuplicateMisplacedAndOutOfOrderImports()
        {
            // 各違反を1つずつ含むtss
            // A tss with one of each violation
            const string a = "Assets/Scripts/Client.Ui/Components/Controls/A/A.uss";
            const string b = "Assets/Scripts/Client.Ui/Components/Panels/B/B.uss";
            const string c = "Assets/Scripts/Client.Ui/Screens/C/C.uss";
            var tss = string.Join("\n",
                "@import url(\"project://database/Assets/Scripts/Client.Ui/Screens/C/C.uss\");",
                "/* == section: Components/Controls == */",
                $"@import url(\"project://database/{a}\");",
                $"@import url(\"project://database/{a}\");",
                $"@import url(\"project://database/{b}\");",
                "/* == section: Components/Panels == */");
            var violations = UiThemeImportBundleRules.CheckBundle(tss, new[] { a, b, c });
            Assert.IsTrue(violations.Any(v => v.Contains("first import must be")), string.Join("\n", violations));
            Assert.IsTrue(violations.Any(v => v.Contains($"{a} is imported 2 times")), string.Join("\n", violations));
            Assert.IsTrue(violations.Any(v => v.Contains($"'{b}' is outside its section")), string.Join("\n", violations));
            Assert.IsTrue(violations.Any(v => v.Contains("'Components/Panels' is out of order")), string.Join("\n", violations));
            Assert.IsTrue(violations.Any(v => v.Contains($"{c} is imported 0 times")), string.Join("\n", violations));
        }

        [Test]
        public void RulesReportSharedRootClass()
        {
            var texts = new Dictionary<string, string>
            {
                { "Assets/Scripts/Client.Ui/Components/Controls/A/A.uss", "/* web: x */\n.same { }" },
                { "Assets/Scripts/Client.Ui/Screens/B/B.uss", ".same .child { }" },
                { "Assets/Scripts/Client.Ui/Foundation/Theme/Theme.uss", ".same { }" },
            };
            var violations = UiThemeImportBundleRules.CheckUniqueRootClasses(texts);
            Assert.AreEqual(1, violations.Count, string.Join("\n", violations));
            StringAssert.Contains("root class .same", violations[0]);
        }

        private static Dictionary<string, string> ClientUiUss()
        {
            // アセットパス→本文
            // Asset path to text
            var root = RepositoryRoot();
            return Directory.GetFiles(Path.Combine(root, ClientUiDirectory), "*.uss", SearchOption.AllDirectories)
                .ToDictionary(path => Path.GetRelativePath(Path.Combine(root, "moorestech_client"), path).Replace('\\', '/'), File.ReadAllText);
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        }
    }
}
