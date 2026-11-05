using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Game.InGame.Tutorial.UIHighlight;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Anchor
{
    /// <summary>
    /// AnchorIds が Web の anchorIds.ts と一致することを確かめる（L0。共存中の同期で、Web 撤去時に消す）
    /// Verifies AnchorIds matches Web's anchorIds.ts (L0; a coexistence sync, removed with the Web UI)
    /// </summary>
    public class AnchorIdsWebParityTest
    {
        private const string WebAnchorIdsPath = "moorestech_web/webui/src/shared/tutorialAnchor/anchorIds.ts";

        [Test]
        public void StaticIdsMatchWeb()
        {
            CollectionAssert.AreEquivalent(ReadWebValues("TutorialAnchorIds"), AnchorIds.StaticIds);
        }

        [Test]
        public void DynamicPrefixesMatchWeb()
        {
            CollectionAssert.AreEquivalent(ReadWebValues("TutorialAnchorDynamicPrefixes"), AnchorIds.DynamicPrefixes);
        }

        [Test]
        public void GeneratorsMatchWebFormats()
        {
            Assert.AreEqual("research.node-abc", AnchorIds.ResearchNode("ABC"));
            Assert.AreEqual("recipe.item-42", AnchorIds.RecipeItem(42));
            Assert.AreEqual("build-menu.entry-block-x", AnchorIds.BuildMenuEntry("Block", "X"));
            Assert.AreEqual("challenge.node-abc", AnchorIds.ChallengeNode("ABC"));
            Assert.AreEqual("inventory.item-abc", AnchorIds.InventoryItem("ABC"));
            Assert.AreEqual("equipment.slot-3", AnchorIds.EquipmentSlot(3));
        }

        private static List<string> ReadWebValues(string objectName)
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var text = File.ReadAllText(Path.Combine(root, WebAnchorIdsPath));

            // オブジェクトリテラルの本体から "..." の値だけを拾う
            // Pick only the quoted values from the object literal body
            var body = Regex.Match(text, objectName + @" = \{(.*?)\} as const", RegexOptions.Singleline);
            Assert.IsTrue(body.Success, $"{objectName} not found in {WebAnchorIdsPath}");
            var values = Regex.Matches(body.Groups[1].Value, "\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
            Assert.IsNotEmpty(values);
            return values;
        }
    }
}
