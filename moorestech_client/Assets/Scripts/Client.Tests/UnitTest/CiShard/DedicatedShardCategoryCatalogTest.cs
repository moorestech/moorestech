using NUnit.Framework;

namespace Client.Tests.UnitTest.CiShard
{
    // shard filterスクリプトの配列からCategory集合を導く規則を固定する
    // Pins the rule that derives the category set from the shard filter script's array
    public class DedicatedShardCategoryCatalogTest
    {
        [Test]
        public void 配列の各shard名をshard_category_nameと同じ規則でCategory名にする()
        {
            var script = "remainder_shards=(\n  client-remainder\n)\ndedicated_shards=(\n  client-play-1 # note\n  client-near-field-startup\n  server-map-2\n)\n";
            Assert.That(DedicatedShardCategoryCatalog.Parse(script), Is.EquivalentTo(new[] { "CiShardClientPlay1", "CiShardClientNearFieldStartup", "CiShardServerMap2" }));
        }

        [Test]
        public void 実スクリプトから専用shardのCategoryを読める()
        {
            // 接頭辞が同じでも専用shardでない名前は含まない（完全一致で判定する前提）
            // A name sharing the prefix but not a dedicated shard is excluded, the premise of exact matching
            var categories = DedicatedShardCategoryCatalog.Load();
            Assert.That(categories, Does.Contain("CiShardClientPlay1"));
            Assert.That(categories, Does.Not.Contain("CiShardClientRemainder"));
        }
    }
}
