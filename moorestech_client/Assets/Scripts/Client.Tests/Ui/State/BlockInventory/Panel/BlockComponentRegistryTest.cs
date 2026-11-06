using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Client.Ui.State.BlockInventory.Logic.Panel;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.Panel
{
    /// <summary>
    /// Web の blockComponentRegistry.test.ts の写し（L0）。登録一覧は Web の registeredBlockTypes.ts と突き合わせる（共存中の同期。Web 撤去時に一覧を C# 側へ移す）
    /// Port of Web blockComponentRegistry.test.ts (L0); the registered list is matched against Web registeredBlockTypes.ts (a coexistence sync, moved to C# when the Web UI goes)
    /// </summary>
    public class BlockComponentRegistryTest
    {
        private const string WebRegisteredBlockTypesPath = "moorestech_web/webui/src/features/blockInventory/registry/registeredBlockTypes.ts";
        private static readonly BlockInventoryBodyKind Fallback = BlockComponentRegistry.Resolve("unknown").Body;

        /// <web-test file="src/features/blockInventory/registry/blockComponentRegistry.test.ts">標準ビューのblockTypeは単一のフォールバックへ統合する</web-test>
        [Test]
        public void 標準ビューの種類は単一のフォールバックへまとめる()
        {
            Assert.AreEqual(Fallback, BlockComponentRegistry.Resolve("Chest").Body);
            Assert.AreEqual(Fallback, BlockComponentRegistry.Resolve("ElectricMachine").Body);
            Assert.AreEqual(Fallback, BlockComponentRegistry.Resolve("GearMiner").Body);
        }

        /// <web-test file="src/features/blockInventory/registry/blockComponentRegistry.test.ts">小文字 chest は実マスタ値でないため fallback になる</web-test>
        [Test]
        public void 小文字のchestは実マスタ値でないのでフォールバック()
        {
            Assert.AreEqual(BlockInventoryBodyKind.SectionStack, BlockComponentRegistry.Resolve("chest").Body);
        }

        /// <web-test file="src/features/blockInventory/registry/blockComponentRegistry.test.ts">tank は登録キー削除済みのためフォールバックを返す</web-test>
        [Test]
        public void tankは登録を消したのでフォールバック()
        {
            Assert.AreEqual(BlockInventoryBodyKind.SectionStack, BlockComponentRegistry.Resolve("tank").Body);
        }

        /// <web-test file="src/features/blockInventory/registry/blockComponentRegistry.test.ts">未登録 blockType はフォールバックを返す</web-test>
        [Test]
        public void 未登録の種類はフォールバック()
        {
            Assert.AreEqual(BlockInventoryBodyKind.SectionStack, BlockComponentRegistry.Resolve("unknown").Body);
            Assert.AreEqual(BlockInventoryTitleActionKind.None, BlockComponentRegistry.Resolve("unknown").TitleAction);
        }

        /// <web-test file="src/features/blockInventory/registry/blockComponentRegistry.test.ts">GearEnergyTransformerUI対象の%sを既存ギアビューへ明示登録する</web-test>
        [TestCase("Shaft")]
        [TestCase("Gear")]
        [TestCase("GearBeltConveyor")]
        public void 歯車系の種類を既存の歯車ビューへ明示登録する(string blockType)
        {
            Assert.IsTrue(BlockComponentRegistry.Registered.TryGetValue(blockType, out var components));
            Assert.AreEqual(BlockInventoryBodyKind.SectionStack, components.Body);
            Assert.AreEqual(BlockInventoryTitleActionKind.None, components.TitleAction);
        }

        /// <web-test file="src/features/blockInventory/registry/blockComponentRegistry.test.ts">スキーマに存在しないGearEnergyTransformerキーは登録しない</web-test>
        [Test]
        public void スキーマに無いGearEnergyTransformerは登録しない()
        {
            Assert.IsFalse(BlockComponentRegistry.Registered.ContainsKey("GearEnergyTransformer"));
        }

        /// <web-test file="src/features/blockInventory/registry/blockComponentRegistry.test.ts">ElectricToGearGeneratorは専用ビューへ登録する</web-test>
        [Test]
        public void 電力歯車変換機は専用ビューへ登録する()
        {
            Assert.IsTrue(BlockComponentRegistry.Registered.ContainsKey("ElectricToGearGenerator"));
            Assert.AreNotEqual(BlockInventoryBodyKind.SectionStack, BlockComponentRegistry.Resolve("ElectricToGearGenerator").Body);
        }

        /// <web-test file="src/features/blockInventory/registry/blockComponentRegistry.test.ts">SpaceElevatorBaseは接続ボタンを持つ専用ビューへ登録する</web-test>
        [Test]
        public void 宇宙エレベーターは専用ビューへ登録する()
        {
            Assert.IsTrue(BlockComponentRegistry.Registered.ContainsKey("SpaceElevatorBase"));
            Assert.AreNotEqual(BlockInventoryBodyKind.SectionStack, BlockComponentRegistry.Resolve("SpaceElevatorBase").Body);
        }

        /// <web-test file="src/features/blockInventory/registry/blockComponentRegistry.test.ts">%sはPhase B2専用ビューへ登録する</web-test>
        [TestCase("TrainStation")]
        [TestCase("TrainItemPlatform")]
        [TestCase("TrainFluidPlatform")]
        [TestCase("ElectricPole")]
        public void 駅と電柱は専用ビューへ登録する(string blockType)
        {
            Assert.IsTrue(BlockComponentRegistry.Registered.ContainsKey(blockType));
            Assert.AreNotEqual(BlockInventoryBodyKind.SectionStack, BlockComponentRegistry.Resolve(blockType).Body);
        }

        /// <web-test file="src/features/blockInventory/registry/blockComponentRegistry.test.ts">網羅e2eが参照する登録blockType一覧と実レジストリを一致させる</web-test>
        [Test]
        public void 登録一覧がWebのregisteredBlockTypesと一致する()
        {
            CollectionAssert.AreEquivalent(ReadWebRegisteredBlockTypes(), BlockComponentRegistry.Registered.Keys);
        }

        private static List<string> ReadWebRegisteredBlockTypes()
        {
            // repo 直下からの相対パスで Web の一覧を読む（前例 AnchorIdsWebParityTest）
            // Read the Web list by its repo-relative path (precedent: AnchorIdsWebParityTest)
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var text = File.ReadAllText(Path.Combine(root, WebRegisteredBlockTypesPath));
            var body = Regex.Match(text, @"registeredBlockTypes = \[(.*?)\] as const", RegexOptions.Singleline);
            Assert.IsTrue(body.Success, $"registeredBlockTypes not found in {WebRegisteredBlockTypesPath}");

            var types = new List<string>();
            foreach (Match match in Regex.Matches(body.Groups[1].Value, "\"([^\"]+)\"")) types.Add(match.Groups[1].Value);
            Assert.IsNotEmpty(types);
            return types;
        }
    }
}
