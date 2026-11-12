using Client.Game.InGame.BlockInfoPolling;
using Client.Ui.State.BlockInventory.Common;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Common
{
    /// <summary>
    /// 区画の値の 3 状態（無し・取得できない・在る）。文脈の欄の「取得できません」の行は取得できないの状態から出る
    /// The three section-value states (absent, unavailable, present); the context entries' unavailable line comes from the unavailable state
    /// </summary>
    public class SectionContentTest
    {
        [Test]
        public void 無しは欄ごと出さず取得状態も持たない()
        {
            var content = SectionContent<int>.Absent;

            Assert.IsTrue(content.IsAbsent);
            Assert.IsFalse(content.IsPresent);
            Assert.IsFalse(content.TryGetUnavailable(out _));
        }

        [Test]
        public void 取得できないは欄を出し値は持たず取得状態を返す()
        {
            var content = SectionContent<int>.Unavailable(BlockInfoFetchState.TransportFailed);

            Assert.IsFalse(content.IsAbsent);
            Assert.IsFalse(content.IsPresent);
            Assert.IsTrue(content.TryGetUnavailable(out var fetchState));
            Assert.AreEqual(BlockInfoFetchState.TransportFailed, fetchState);
        }

        [Test]
        public void 在るは値を持ち取得できないではない()
        {
            var content = SectionContent<int>.Present(7);

            Assert.IsFalse(content.IsAbsent);
            Assert.IsTrue(content.IsPresent);
            Assert.AreEqual(7, content.Value);
            Assert.IsFalse(content.TryGetUnavailable(out _));
        }

        [Test]
        public void 同じ状態は等しく購読の重複配信を起こさない()
        {
            Assert.AreEqual(SectionContent<int>.Absent, SectionContent<int>.Absent);
            Assert.AreEqual(SectionContent<int>.Unavailable(BlockInfoFetchState.NotFetched), SectionContent<int>.Unavailable(BlockInfoFetchState.NotFetched));
            Assert.AreNotEqual(SectionContent<int>.Unavailable(BlockInfoFetchState.NotFetched), SectionContent<int>.Unavailable(BlockInfoFetchState.Vanished));
        }
    }
}
