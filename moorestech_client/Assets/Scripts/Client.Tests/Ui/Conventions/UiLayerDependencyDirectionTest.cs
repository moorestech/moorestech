// moorestech_client/Assets/Scripts/Client.Tests/Ui/Conventions/UiLayerDependencyDirectionTest.cs
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Conventions
{
    /// <summary>
    /// 層の参照の向きを検査（L2）
    /// Checks the layer reference direction (L2)
    /// </summary>
    public class UiLayerDependencyDirectionTest
    {
        private const string ClientUiDirectory = "moorestech_client/Assets/Scripts/Client.Ui";

        [Test]
        public void ClientUiLayersReferenceOnlyAllowedDirections()
        {
            var root = Path.Combine(RepositoryRoot(), ClientUiDirectory);
            var violations = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .SelectMany(path => UiLayerDependencyRules.FindViolations(Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllText(path)))
                .ToList();
            Assert.IsEmpty(violations, string.Join("\n", violations));

            #region Internal

            string RepositoryRoot()
            {
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            }

            #endregion
        }

        [Test]
        public void RulesCatchUsingsAndQualifiedNamesButAllowExceptions()
        {
            // usingと完全修飾名を拾う
            // Catches usings and qualified names
            const string component = "using Client.Game.InGame;\nusing Client.Ui.State.Text;\n// Client.Ui.Screens in a comment\nclass A { Client.Ui.State.Toast.GameUiToast t; }";
            var componentViolations = UiLayerDependencyRules.FindViolations("Components/Controls/A/A.cs", component);
            Assert.AreEqual(2, componentViolations.Count, string.Join("\n", componentViolations));

            const string foundation = "using Client.Ui.State.Routing;\nusing Client.Ui.Components.Controls;";
            var foundationViolations = UiLayerDependencyRules.FindViolations("Foundation/Root/B.cs", foundation);
            Assert.AreEqual(1, foundationViolations.Count, string.Join("\n", foundationViolations));

            Assert.IsEmpty(UiLayerDependencyRules.FindViolations("Screens/PauseMenu/C.cs", component));
            Assert.AreEqual(1, UiLayerDependencyRules.FindViolations("Content/D.cs", "using Client.Ui.Screens.Toast;").Count);
        }
    }
}
