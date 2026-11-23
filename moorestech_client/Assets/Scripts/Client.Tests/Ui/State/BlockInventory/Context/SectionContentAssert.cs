using Client.Game.InGame.BlockInfoPolling;
using Client.Ui.State.BlockInventory.Common;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 欄の3値（在る・取得不可・無し）のうち在る・取得不可を取り出して確かめる
    /// Extracts and asserts the present or unavailable state of an entry
    /// </summary>
    public static class SectionContentAssert
    {
        public static T Present<T>(SectionContent<T> entry)
        {
            Assert.IsTrue(entry.TryGetPresent(out var value), "取得済みの欄である");
            return value;
        }

        public static BlockInfoFetchState Unavailable<T>(SectionContent<T> entry)
        {
            Assert.IsTrue(entry.TryGetUnavailable(out var fetchState), "取得できない欄である");
            return fetchState;
        }
    }
}
