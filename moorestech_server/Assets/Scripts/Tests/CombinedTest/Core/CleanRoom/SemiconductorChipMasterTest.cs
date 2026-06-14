using System;
using System.Collections.Generic;
using Core.Master;
using Newtonsoft.Json.Linq;
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

        // 有効なテストMODのマスタは Validate を通る（非回帰ガード）。
        // The valid test-mod master passes Validate (regression guard).
        [Test]
        public void Validate_ValidMaster_Passes()
        {
            var master = new SemiconductorChipMaster(ValidJToken());
            var ok = master.Validate(out var msg);
            Assert.IsTrue(ok, msg);
            Assert.IsNull(msg);
        }

        // levelWeights が chipLevels に無い level を参照すると Validate が失敗する。
        // Validate fails when levelWeights references a level absent from chipLevels.
        [Test]
        public void Validate_LevelWeightUnknownLevel_Fails()
        {
            var token = ValidJToken();
            // 存在しない level=99 を分布に混入させる。
            // Inject a level=99 that does not exist in chipLevels.
            ((JArray)token["outputDistributions"][0]["levelWeights"]).Add(
                new JObject { ["level"] = 99, ["weight"] = 0.5 });

            AssertValidateFails(token);
        }

        // weight が 0 以下だと Validate が失敗する。
        // Validate fails when a weight is non-positive.
        [Test]
        public void Validate_NonPositiveWeight_Fails()
        {
            var token = ValidJToken();
            token["outputDistributions"][0]["levelWeights"][0]["weight"] = 0.0;

            AssertValidateFails(token);
        }

        // (machineRecipeGuid, outputItemGuid) が重複すると Validate が失敗する。
        // Validate fails on duplicate (machineRecipeGuid, outputItemGuid) keys.
        [Test]
        public void Validate_DuplicateDistributionKey_Fails()
        {
            var token = ValidJToken();
            // 同一キーの分布をもう1つ追加する。
            // Append a second distribution with the same key.
            var dup = (JObject)token["outputDistributions"][0].DeepClone();
            ((JArray)token["outputDistributions"]).Add(dup);

            AssertValidateFails(token);
        }

        // chipLevels が空だと Validate が失敗する。
        // Validate fails when chipLevels is empty.
        [Test]
        public void Validate_EmptyChipLevels_Fails()
        {
            var token = ValidJToken();
            ((JArray)token["chipLevels"]).Clear();

            AssertValidateFails(token);
        }

        // chipLevels の level が重複すると Validate が失敗する。
        // Validate fails on duplicate chipLevels level.
        [Test]
        public void Validate_DuplicateChipLevel_Fails()
        {
            var token = ValidJToken();
            var dup = (JObject)token["chipLevels"][0].DeepClone();
            ((JArray)token["chipLevels"]).Add(dup);

            AssertValidateFails(token);
        }

        // Validate が false かつメッセージ非空であることを確認する共通アサート。
        // Shared assert: Validate returns false with a non-empty message.
        private static void AssertValidateFails(JToken token)
        {
            var master = new SemiconductorChipMaster(token);
            var ok = master.Validate(out var msg);
            Assert.IsFalse(ok);
            Assert.IsNotEmpty(msg);
        }

        // テストMOD相当の有効な JToken を生成する（テスト毎に独立コピー）。
        // Builds a valid test-mod-equivalent JToken (independent copy per test).
        private static JToken ValidJToken()
        {
            return JObject.Parse(@"
            {
              ""chipLevels"": [
                { ""level"": 1, ""itemGuid"": ""3a000000-0000-0000-0000-000000000001"" },
                { ""level"": 2, ""itemGuid"": ""3a000000-0000-0000-0000-000000000002"" },
                { ""level"": 3, ""itemGuid"": ""3a000000-0000-0000-0000-000000000003"" },
                { ""level"": 4, ""itemGuid"": ""3a000000-0000-0000-0000-000000000004"" }
              ],
              ""outputDistributions"": [
                {
                  ""machineRecipeGuid"": ""3c000000-0000-0000-0000-000000000001"",
                  ""outputItemGuid"": ""3a000000-0000-0000-0000-000000000001"",
                  ""levelWeights"": [
                    { ""level"": 1, ""weight"": 0.70 },
                    { ""level"": 2, ""weight"": 0.20 },
                    { ""level"": 3, ""weight"": 0.08 },
                    { ""level"": 4, ""weight"": 0.02 }
                  ]
                }
              ]
            }");
        }
    }
}
