using Client.Ui.Screens.BlockInventory.Parts.Rows;
using NUnit.Framework;

namespace Client.Tests.Ui.Screens.BlockInventory.Parts
{
    /// <summary>
    /// 鍵の列の比較 1 本（採掘機・ポンプの毎分の行と枠の並びが共有する）は件数と添字ごとの鍵で比べる
    /// The single keyed-list comparison (shared by miner/pump rows and slot lists) compares count and per-index keys
    /// </summary>
    public class KeyedRowListTest
    {
        [Test]
        public void 同じ並びの鍵なら同じで行は残る()
        {
            var list = Filled(3, 5, 5);

            Assert.IsTrue(list.HasSameKeys(new[] { 3, 5, 5 }));
            Assert.AreEqual("row-3", list.RowAt(0));
            Assert.AreEqual(5, list.KeyAt(2));
        }

        [TestCase(new[] { 3, 5 }, TestName = "件数が減れば違う")]
        [TestCase(new[] { 3, 5, 5, 7 }, TestName = "件数が増えれば違う")]
        [TestCase(new[] { 5, 3, 5 }, TestName = "同じ鍵の集まりでも並びが違えば違う")]
        [TestCase(new[] { 3, 5, 6 }, TestName = "1 つでも鍵が違えば違う")]
        public void 並びが変わったら違う(int[] next)
        {
            Assert.IsFalse(Filled(3, 5, 5).HasSameKeys(next));
        }

        [Test]
        public void 空にすると空の並びと同じ()
        {
            var list = Filled(3);
            list.Clear();

            Assert.AreEqual(0, list.Count);
            Assert.IsTrue(list.HasSameKeys(new int[0]));
        }

        private static KeyedRowList<int, string> Filled(params int[] keys)
        {
            var list = new KeyedRowList<int, string>();
            foreach (var key in keys) list.Add(key, $"row-{key}");
            return list;
        }
    }
}
