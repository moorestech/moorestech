using System;
using Core.Master;
using Mooresmaster.Loader;
using Mooresmaster.Model.BuildMenuModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Core.Block
{
    /// <summary>
    ///     カテゴリ/サブカテゴリ定義のバリデーションと参照判定を検証するテスト
    ///     Tests verifying validation and reference lookup for category/subCategory definitions
    /// </summary>
    public class BuildMenuCategoryMasterTest
    {
        // 非ブロックentrySourceを全て満たす定義断片（各テストのカテゴリに追記して使う）
        // A fragment defining every non-blocks entrySource, appended to each test's categories
        private const string NonBlockCategories = @"
            {""name"":""輸送"",""subCategories"":[{""name"":""車両"",""entrySource"":""trainCars""}]},
            {""name"":""ツール"",""subCategories"":[
                {""name"":""接続"",""entrySource"":""connectTools""},
                {""name"":""ブループリント"",""entrySource"":""blueprintCopyTool""}]},
            {""name"":""ブループリント"",""subCategories"":[{""name"":""保存済み"",""entrySource"":""savedBlueprints""}]}";

        private static JToken CreateJson(string categoriesJson)
        {
            var json = JToken.Parse($@"{{""blueprintInitialUnlocked"":false,""categories"":[{categoriesJson},{NonBlockCategories}],""connectTools"":[],""buildTools"":[]}}");
            AddRequiredGuids(json);
            return json;
        }

        [Test]
        public void 重複カテゴリ名はバリデーションで失敗する()
        {
            var json = CreateJson(@"
                {""name"":""採掘"",""subCategories"":[{""name"":""採掘機"",""entrySource"":""blocks""}]},
                {""name"":""採掘"",""subCategories"":[{""name"":""液体採取"",""entrySource"":""blocks""}]}");
            var master = new BuildMenuCategoryMaster(json);
            Assert.IsFalse(master.Validate(out var logs));
            Assert.IsTrue(logs.Contains("duplicate"));
        }

        [Test]
        public void カテゴリ内サブカテゴリ重複はバリデーションで失敗する()
        {
            var json = CreateJson(@"
                {""name"":""採掘"",""subCategories"":[{""name"":""採掘機"",""entrySource"":""blocks""},{""name"":""採掘機"",""entrySource"":""blocks""}]}");
            var master = new BuildMenuCategoryMaster(json);
            Assert.IsFalse(master.Validate(out _));
        }

        [Test]
        public void 定義済みペアはContainsがtrueを返す()
        {
            var json = CreateJson(@"
                {""name"":""採掘"",""subCategories"":[{""name"":""採掘機"",""entrySource"":""blocks""}]}");
            var master = new BuildMenuCategoryMaster(json);
            Assert.IsTrue(master.Validate(out _));
            master.Initialize();
            Assert.IsTrue(master.Contains("採掘", "採掘機"));
            Assert.IsFalse(master.Contains("採掘", "未定義"));
            Assert.IsFalse(master.Contains("未定義", "採掘機"));
        }

        [Test]
        public void 非ブロックentrySourceの欠落はバリデーションで失敗する()
        {
            var json = JToken.Parse(@"{""blueprintInitialUnlocked"":false,""categories"":[
                {""name"":""採掘"",""subCategories"":[{""name"":""採掘機"",""entrySource"":""blocks""}]}],""connectTools"":[],""buildTools"":[]}");
            AddRequiredGuids(json);
            var master = new BuildMenuCategoryMaster(json);
            Assert.IsFalse(master.Validate(out var logs));
            Assert.IsTrue(logs.Contains("entrySource"));
        }

        [Test]
        public void entrySourceからカテゴリペアを逆引きできる()
        {
            var json = CreateJson(@"
                {""name"":""採掘"",""subCategories"":[{""name"":""採掘機"",""entrySource"":""blocks""}]}");
            var master = new BuildMenuCategoryMaster(json);
            Assert.IsTrue(master.Validate(out _));
            master.Initialize();
            var expectedCategoryGuid = Guid.Parse(json["categories"]![1]!["categoryGuid"]!.Value<string>());
            var expectedSubCategoryGuid = Guid.Parse(
                json["categories"]![1]!["subCategories"]![0]!["subCategoryGuid"]!.Value<string>());
            var (categoryGuid, subCategoryGuid) = master.GetPairByEntrySource(BuildMenuSubCategoryElement.EntrySourceConst.trainCars);
            Assert.AreEqual(expectedCategoryGuid, categoryGuid);
            Assert.AreEqual(expectedSubCategoryGuid, subCategoryGuid);
        }

        [Test]
        public void 定義名ペアからGuidペアを逆引きできる()
        {
            var json = CreateJson(@"
                {""name"":""採掘"",""subCategories"":[{""name"":""採掘機"",""entrySource"":""blocks""}]}");
            var master = new BuildMenuCategoryMaster(json);
            master.Initialize();

            var expectedCategoryGuid = Guid.Parse(json["categories"]![0]!["categoryGuid"]!.Value<string>());
            var expectedSubCategoryGuid = Guid.Parse(
                json["categories"]![0]!["subCategories"]![0]!["subCategoryGuid"]!.Value<string>());
            var actual = master.GetGuidPair("採掘", "採掘機");
            Assert.AreEqual((expectedCategoryGuid, expectedSubCategoryGuid), actual);
        }

        [Test]
        public void categoryGuid欠落はローダーで拒否する()
        {
            var json = JToken.Parse(@"{""blueprintInitialUnlocked"":false,""categories"":[
                {""name"":""採掘"",""subCategories"":[
                    {""subCategoryGuid"":""20000000-0000-4000-8000-000000000001"",""name"":""採掘機"",""entrySource"":""blocks""}]}],
                ""connectTools"":[],""buildTools"":[]}");
            Assert.Throws<MooresmasterLoaderException>(() => new BuildMenuCategoryMaster(json));
        }

        [Test]
        public void カテゴリとサブカテゴリを跨ぐGuid重複は拒否する()
        {
            var json = CreateJson(@"
                {""name"":""採掘"",""subCategories"":[{""name"":""採掘機"",""entrySource"":""blocks""}]}");
            json["categories"]![0]!["subCategories"]![0]!["subCategoryGuid"] =
                json["categories"]![0]!["categoryGuid"];

            var master = new BuildMenuCategoryMaster(json);
            Assert.IsFalse(master.Validate(out var logs));
            StringAssert.Contains("duplicate", logs);
        }

        private static void AddRequiredGuids(JToken json)
        {
            var categoryIndex = 0;
            foreach (var category in json["categories"]!)
            {
                categoryIndex++;
                category["categoryGuid"] = $"10000000-0000-4000-8000-{categoryIndex:D12}";
                var subCategoryIndex = 0;
                foreach (var subCategory in category["subCategories"]!)
                {
                    subCategoryIndex++;
                    subCategory["subCategoryGuid"] = $"20000000-0000-4000-{categoryIndex:D4}-{subCategoryIndex:D12}";
                }
            }
        }
    }
}
