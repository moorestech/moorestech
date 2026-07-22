using Core.Master;
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
            return JToken.Parse($@"{{""categories"":[{categoriesJson},{NonBlockCategories}],""connectTools"":[]}}");
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
            var json = JToken.Parse(@"{""categories"":[
                {""name"":""採掘"",""subCategories"":[{""name"":""採掘機"",""entrySource"":""blocks""}]}],""connectTools"":[]}");
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
            var (category, subCategory) = master.GetPairByEntrySource(BuildMenuSubCategoryElement.EntrySourceConst.trainCars);
            Assert.AreEqual("輸送", category);
            Assert.AreEqual("車両", subCategory);
        }
    }
}
