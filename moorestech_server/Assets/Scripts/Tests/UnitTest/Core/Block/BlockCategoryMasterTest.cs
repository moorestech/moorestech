using Core.Master;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Core.Block
{
    /// <summary>
    ///     カテゴリ/サブカテゴリ定義のバリデーションと参照判定を検証するテスト
    ///     Tests verifying validation and reference lookup for category/subCategory definitions
    /// </summary>
    public class BlockCategoryMasterTest
    {
        [Test]
        public void 重複カテゴリ名はバリデーションで失敗する()
        {
            var json = JToken.Parse(@"{""data"":[
                {""name"":""採掘"",""subCategories"":[{""name"":""採掘機""}]},
                {""name"":""採掘"",""subCategories"":[{""name"":""液体採取""}]}]}");
            var master = new BlockCategoryMaster(json);
            Assert.IsFalse(master.Validate(out var logs));
            Assert.IsTrue(logs.Contains("duplicate"));
        }

        [Test]
        public void カテゴリ内サブカテゴリ重複はバリデーションで失敗する()
        {
            var json = JToken.Parse(@"{""data"":[
                {""name"":""採掘"",""subCategories"":[{""name"":""採掘機""},{""name"":""採掘機""}]}]}");
            var master = new BlockCategoryMaster(json);
            Assert.IsFalse(master.Validate(out var logs));
        }

        [Test]
        public void 定義済みペアはContainsがtrueを返す()
        {
            var json = JToken.Parse(@"{""data"":[
                {""name"":""採掘"",""subCategories"":[{""name"":""採掘機""}]}]}");
            var master = new BlockCategoryMaster(json);
            Assert.IsTrue(master.Validate(out _));
            master.Initialize();
            Assert.IsTrue(master.Contains("採掘", "採掘機"));
            Assert.IsFalse(master.Contains("採掘", "未定義"));
            Assert.IsFalse(master.Contains("未定義", "採掘機"));
        }
    }
}
