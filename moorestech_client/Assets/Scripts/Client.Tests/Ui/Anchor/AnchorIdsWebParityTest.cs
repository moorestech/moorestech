using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Game.InGame.Tutorial.UIHighlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.Anchor
{
    /// <summary>
    /// AnchorIds が Web の anchorIds.ts と一致することを確かめる（L0。共存中の同期で、Web 撤去時に消す）
    /// Verifies AnchorIds matches Web's anchorIds.ts (L0; a coexistence sync, removed with the Web UI)
    /// </summary>
    public class AnchorIdsWebParityTest
    {
        private const string WebAnchorIdsPath = "moorestech_web/webui/src/shared/tutorialAnchor/anchorIds.ts";

        /// <web-test file="src/shared/tutorialAnchor/anchorIds.test.ts">static IDs match the shared fixture exactly</web-test>
        [Test]
        public void StaticIdsMatchWeb()
        {
            CollectionAssert.AreEquivalent(ReadWebValues("TutorialAnchorIds"), AnchorIds.StaticIds);
        }

        /// <web-test file="src/shared/tutorialAnchor/anchorIds.test.ts">dynamic ID prefixes match the shared fixture exactly</web-test>
        [Test]
        public void DynamicPrefixesMatchWeb()
        {
            CollectionAssert.AreEquivalent(ReadWebValues("TutorialAnchorDynamicPrefixes"), AnchorIds.DynamicPrefixes);
        }

        /// <web-test file="src/shared/tutorialAnchor/anchorIds.test.ts">lowercases guid-suffixed IDs and keeps the numeric suffixes intact</web-test>
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

        /// <web-test file="src/shared/tutorialAnchor/anchorIds.test.ts">rejects whitespace in the generated ID</web-test>
        [Test]
        public void WhitespaceInGeneratedIdLogsError()
        {
            // 空白入りIDはクラス名トークンとして解決しないので、生成時にエラーログで開発者へ知らせる
            // A whitespace-bearing ID never resolves as a class token, so generation reports it to developers via an error log
            LogAssert.Expect(LogType.Error, new Regex(@"\[AnchorIds\] anchor id must not contain whitespace: ""inventory\.item-a0000000-0000-4000-8000-000000000001 extra"""));
            LogAssert.Expect(LogType.Error, new Regex(@"\[AnchorIds\] anchor id must not contain whitespace: ""build-menu\.entry-block-iron chest"""));
            AnchorIds.InventoryItem("a0000000-0000-4000-8000-000000000001 extra");
            AnchorIds.BuildMenuEntry("block", "iron chest");
            LogAssert.NoUnexpectedReceived();
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
