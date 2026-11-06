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
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.BlockInfoNotFetched.Key, BlockInfoFetchStateLogic.UnavailableKey(BlockInfoUnavailableState.NotFetched).Key);
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.BlockInfoTransportFailed.Key, BlockInfoFetchStateLogic.UnavailableKey(BlockInfoUnavailableState.TransportFailed).Key);
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.BlockInfoUnreadable.Key, BlockInfoFetchStateLogic.UnavailableKey(BlockInfoUnavailableState.Unreadable).Key);
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.BlockInfoUnsupportedTarget.Key, BlockInfoFetchStateLogic.UnavailableKey(BlockInfoUnavailableState.UnsupportedTarget).Key);
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.BlockInfoVanished.Key, BlockInfoFetchStateLogic.UnavailableKey(BlockInfoUnavailableState.Vanished).Key);
        }
    }
}
