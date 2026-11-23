using System.Linq;
using NUnit.Framework;
using static Client.Tests.Ui.State.BlockInventory.Special.Cargo.OrbitalCargoSamples;

namespace Client.Tests.Ui.State.BlockInventory.Special.Cargo
{
    public class OrbitalCargoDetailTest
    {
        [Test]
        public void 一覧は運ぶ種類のうち解放済みだけを運ぶ順で出す()
        {
            var detail = Pad(null, null, Cable, ChipLv1);
            CollectionAssert.AreEqual(new[] { ChipLv1, Cable }, detail.SelectableChoices.Select(c => c.PayloadItemId).ToArray());
            Assert.IsFalse(detail.IsSelected);
            Assert.IsFalse(detail.TryGetHeadline(out _), "未選択で今の便も無ければ見出しは無い");
        }

        [Test]
        public void 見出しは今の便を優先し選んだ積荷と違えば次の便を出す()
        {
            var detail = Pad(ChipLv2, ChipLv1, ChipLv1, ChipLv2);
            Assert.IsTrue(detail.TryGetHeadline(out var headline));
            Assert.AreEqual(ChipLv1, headline.PayloadItemId);
            Assert.IsTrue(detail.TryGetNextFlight(out var next));
            Assert.AreEqual(ChipLv2, next.PayloadItemId);
        }

        [Test]
        public void 今の便が選んだ積荷と同じか無ければ次の便を出さない()
        {
            Assert.IsFalse(Pad(ChipLv1, ChipLv1, ChipLv1).TryGetNextFlight(out _));
            var idle = Pad(ChipLv1, null, ChipLv1);
            Assert.IsFalse(idle.TryGetNextFlight(out _));
            Assert.IsTrue(idle.TryGetHeadline(out var headline));
            Assert.AreEqual(ChipLv1, headline.PayloadItemId);
        }

        [Test]
        public void 古いセーブで未選択のまま今の便があれば見出しは今の便で一覧も出す()
        {
            // 選択の項目が無いセーブは未選択で読まれ、積み終えた便だけが残る（A の判断）
            // A save without the selection loads as unselected while the loaded flight remains (server draft A)
            var detail = Pad(null, ChipLv1, ChipLv1);
            Assert.IsFalse(detail.IsSelected);
            Assert.IsTrue(detail.TryGetHeadline(out var headline));
            Assert.AreEqual(ChipLv1, headline.PayloadItemId);
            Assert.IsFalse(detail.TryGetNextFlight(out _));
        }
    }
}
