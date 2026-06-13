using System;
using System.Collections.Generic;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core
{
    // SemiconductorChipMaster のスモークテスト。マスタロードと基本 API を確認する。
    // Smoke tests for SemiconductorChipMaster: verifies master load and basic API.
    public class SemiconductorChipMasterTest
    {
        [SetUp]
        public void SetUp()
        {
            // DIコンテナ生成で MasterHolder.Load が走る。
            // Creating the DI container triggers MasterHolder.Load.
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        // GetChipItemId(1) は ForUnitTestItemId.IcChipLv1 と一致する
        // GetChipItemId(1) matches ForUnitTestItemId.IcChipLv1
        [Test]
        public void GetChipItemId_Level1_MatchesIcChipLv1()
        {
            var master = MasterHolder.SemiconductorChipMaster;
            Assert.AreEqual(ForUnitTestItemId.IcChipLv1, master.GetChipItemId(1));
        }

        // GetChipItemId で Lv2..4 も正しく引ける
        // GetChipItemId resolves Lv2 through Lv4 correctly
        [Test]
        public void GetChipItemId_AllLevels_ResolveCorrectly()
        {
            var master = MasterHolder.SemiconductorChipMaster;
            Assert.AreEqual(ForUnitTestItemId.IcChipLv2, master.GetChipItemId(2));
            Assert.AreEqual(ForUnitTestItemId.IcChipLv3, master.GetChipItemId(3));
            Assert.AreEqual(ForUnitTestItemId.IcChipLv4, master.GetChipItemId(4));
        }

        // GetChipLevel は逆引き可能
        // GetChipLevel performs reverse-lookup correctly
        [Test]
        public void GetChipLevel_ReverseLoookup_ReturnsCorrectLevel()
        {
            var master = MasterHolder.SemiconductorChipMaster;
            Assert.AreEqual(1, master.GetChipLevel(ForUnitTestItemId.IcChipLv1));
            Assert.AreEqual(4, master.GetChipLevel(ForUnitTestItemId.IcChipLv4));
        }

        // TryGetDistribution は露光レシピのチップ出力について4要素昇順リストを返す
        // TryGetDistribution returns a 4-element ascending list for the exposure recipe chip output
        [Test]
        public void TryGetDistribution_ExposureRecipeChipOutput_ReturnsFourAscendingWeights()
        {
            var master = MasterHolder.SemiconductorChipMaster;
            var recipeGuid = Guid.Parse("3c000000-0000-0000-0000-000000000001");
            var chipOutputGuid = Guid.Parse("3a000000-0000-0000-0000-000000000001");

            var found = master.TryGetDistribution(recipeGuid, chipOutputGuid, out var dist);

            Assert.IsTrue(found);
            Assert.AreEqual(4, dist.Count);

            // level 昇順のソートを確認（Task 1 の DrawBaseLevel が前提とする順序）
            // Verify ascending level order (required by DrawBaseLevel)
            for (var i = 1; i < dist.Count; i++)
                Assert.Less(dist[i - 1].level, dist[i].level, "levels must be ascending");

            Assert.AreEqual(1, dist[0].level);
            // float 精度（number 型は float 生成）を許容する delta
            // Tolerate float precision (number type generates float)
            Assert.AreEqual(0.70, dist[0].weight, 1e-6);
            Assert.AreEqual(4, dist[3].level);
            Assert.AreEqual(0.02, dist[3].weight, 1e-6);
        }

        // 副産物（チップ以外の出力要素）は TryGetDistribution で false を返す
        // Byproduct output elements (non-chip) return false from TryGetDistribution
        [Test]
        public void TryGetDistribution_ByproductOutput_ReturnsFalse()
        {
            var master = MasterHolder.SemiconductorChipMaster;
            var recipeGuid = Guid.Parse("3c000000-0000-0000-0000-000000000001");
            var byproductGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");

            var found = master.TryGetDistribution(recipeGuid, byproductGuid, out _);

            Assert.IsFalse(found);
        }
    }
}
