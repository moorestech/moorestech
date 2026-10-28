using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Block.Blocks.Machine.State;
using Game.SaveLoad.Pruning;
using Game.SaveLoad.Pruning.Items;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.CombinedTest.Core.Machine.Upgrade;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Core.Machine.Save
{
    public class MachineProcessingRestoreCompatibilityTest
    {
        [SetUp]
        public void Setup()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void MissingConsumedIdentityIsArchivedAndDropsWholeJob()
        {
            var missingGuid = Guid.NewGuid().ToString();
            var saved = new JObject { ["consumedInputItemGuids"] = new JArray(missingGuid, LevelInheritanceTestHelper.Test1ItemGuid.ToString()) };
            var pruned = new MissingMasterSectionPruneResult();
            var walker = new ItemStackPruneWalker(new MissingItemReferenceCleaner(pruned));
            walker.Walk(saved, PrunedItemOrigin.BlockState("world", 1, "machine"));

            // 欠損の退避先とスロットを残し、nullをID解決へ渡さない
            // Preserve the missing identity and its slot, never handing null to id resolution
            Assert.AreEqual(missingGuid, (string)pruned.EmptiedItemStacks[0]["itemGuid"]);
            Assert.AreEqual(0, (int)pruned.EmptiedItemStacks[0]["origin"]["slot"]);
            Assert.AreEqual(JTokenType.Null, saved["consumedInputItemGuids"][0].Type);
            LogAssert.Expect(LogType.Warning, new Regex("消費実品がマスタから除去された"));
            var restored = MachineProcessingJob.RestoreFromFlatSave(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid.ToString(), 0.5, null, null, 20u,
                saved["consumedInputItemGuids"].ToObject<List<string>>(), 1f);
            Assert.IsNull(restored);
        }

        [Test]
        public void ChangedInputCountUsesLegacyConsumptionWithoutLosingFixedTime()
        {
            var consumed = new List<string> { LevelInheritanceTestHelper.Test1ItemGuid.ToString() };
            LogAssert.Expect(LogType.Warning, new Regex("現行レシピと消費実品数が異なる"));
            var restored = MachineProcessingJob.RestoreFromFlatSave(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid.ToString(), 0.5, null, null, 20u, consumed, 2f);

            // 実品記録だけを旧方式へ戻し、確定した加工費用を保つ
            // Fall back only for consumed identities, preserving the fixed job cost
            Assert.IsNotNull(restored);
            Assert.IsNull(restored.ConsumedInputItemIds);
            Assert.AreEqual(20u, restored.TotalTicks);
            Assert.AreEqual(2f, restored.PowerMultiplier);
        }

        [Test]
        public void ChangedInheritanceDropsJobInsteadOfAbortingLoad()
        {
            LogAssert.Expect(LogType.Warning, new Regex("現行レシピと継承設定が異なる"));
            var restored = MachineProcessingJob.RestoreFromFlatSave(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid.ToString(), 0.5, null, 2, 20u, null, 1f);
            Assert.IsNull(restored);
        }
    }
}
