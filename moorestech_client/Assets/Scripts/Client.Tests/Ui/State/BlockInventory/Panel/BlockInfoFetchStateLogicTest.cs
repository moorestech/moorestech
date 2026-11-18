using Client.Game.InGame.BlockInfoPolling;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Panel
{
    /// <summary>
    /// Web の blockInfoFetchStateLogic.test.ts の写し（L0）
    /// Port of Web blockInfoFetchStateLogic.test.ts (L0)
    /// </summary>
    public class BlockInfoFetchStateLogicTest
    {
        /// <web-test file="src/features/blockInventory/details/blockInfoFetch/__tests__/blockInfoFetchStateLogic.test.ts">取得できていない状態ごとに別の文言を選ぶ</web-test>
        [Test]
        public void 取得できていない状態ごとに別の文言を選ぶ()
        {
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.BlockInfoNotFetched.Key, KeyOf(BlockInfoFetchState.NotFetched));
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.BlockInfoTransportFailed.Key, KeyOf(BlockInfoFetchState.TransportFailed));
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.BlockInfoUnreadable.Key, KeyOf(BlockInfoFetchState.Unreadable));
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.BlockInfoUnsupportedTarget.Key, KeyOf(BlockInfoFetchState.UnsupportedTarget));
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.BlockInfoVanished.Key, KeyOf(BlockInfoFetchState.Vanished));
        }

        // 取得済みは文言を持たない
        // Fetched has no message
        [Test]
        public void 取得済みは文言を返さない()
        {
            Assert.IsFalse(BlockInfoFetchStateLogic.TryGetUnavailableKey(BlockInfoFetchState.Fetched, out _));
        }

        private static string KeyOf(BlockInfoFetchState state)
        {
            Assert.IsTrue(BlockInfoFetchStateLogic.TryGetUnavailableKey(state, out var key));
            return key.Key;
        }
    }
}
