using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Client.Game.InGame.Tutorial.UIHighlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.Anchor
{
    /// <summary>
    /// AnchorIds の一覧が定数を過不足なく含み、生成する id の書式が決まった形になることを確かめる（L0）
    /// Verifies the AnchorIds lists hold exactly their constants and generated ids keep their fixed formats (L0)
    /// </summary>
    public class AnchorIdsWebParityTest
    {
        [Test]
        public void StaticIdsListEveryNonPrefixConstant()
        {
            // 一覧から漏れた固定 id は TutorialAnchor.IsAnchorId が印を外し損ねる
            // A static id missing from the list is never recognized by TutorialAnchor.IsAnchorId
            CollectionAssert.AreEquivalent(Constants().Where(field => !field.Name.EndsWith("Prefix")).Select(Value), AnchorIds.StaticIds);
        }

        [Test]
        public void DynamicPrefixesListEveryPrefixConstant()
        {
            // 一覧から漏れた接頭辞は生成した id の印を外し損ねる
            // A prefix missing from the list leaves generated ids unrecognized
            CollectionAssert.AreEquivalent(Constants().Where(field => field.Name.EndsWith("Prefix")).Select(Value), AnchorIds.DynamicPrefixes);
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

        private static FieldInfo[] Constants()
        {
            return typeof(AnchorIds).GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.IsLiteral).ToArray();
        }

        private static string Value(FieldInfo field)
        {
            return (string)field.GetRawConstantValue();
        }
    }
}
