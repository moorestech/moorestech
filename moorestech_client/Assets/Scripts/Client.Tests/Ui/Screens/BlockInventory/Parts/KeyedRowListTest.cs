using Client.Ui.Screens.BlockInventory.Parts.Rows;
using NUnit.Framework;

namespace Client.Tests.Ui.Screens.BlockInventory.Parts
{
    /// <summary>
    /// 鍵列は件数と添字ごとの鍵で比べる
    /// Keys compare by count and per-index key
    /// </summary>
    public class KeyedRowListTest
    {
        [Test]
        public void 同じ並びの鍵なら同じで行は残る()
        {
            var list = Filled(3, 5, 5);

            Assert.IsFalse(list.ClearIfKeysChanged(new[] { 3, 5, 5 }));
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual("row-3", list.RowAt(0));
            Assert.AreEqual(5, list.KeyAt(2));
        }

        [TestCase(new[] { 3, 5 }, TestName = "件数が減れば違う")]
        [TestCase(new[] { 3, 5, 5, 7 }, TestName = "件数が増えれば違う")]
        [TestCase(new[] { 5, 3, 5 }, TestName = "同じ鍵の集まりでも並びが違えば違う")]
        [TestCase(new[] { 3, 5, 6 }, TestName = "1 つでも鍵が違えば違う")]
        public void 並びが変わったら違う(int[] next)
        {
            var list = Filled(3, 5, 5);

            Assert.IsTrue(list.ClearIfKeysChanged(next));
            Assert.AreEqual(0, list.Count);
        }

        [Test]
        public void 空の並びを渡すと空にして変わった扱いになり_次の同じ並びは変わらない()
        {
            var list = Filled(3);

            Assert.IsTrue(list.ClearIfKeysChanged(new int[0]));
            Assert.AreEqual(0, list.Count);
            Assert.IsFalse(list.ClearIfKeysChanged(new int[0]));
        }

        private static KeyedRowList<int, string> Filled(params int[] keys)
        {
            var list = new KeyedRowList<int, string>();
            foreach (var key in keys) list.Add(key, $"row-{key}");
            return list;
        }
    }
}
