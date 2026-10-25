using Client.Game.InGame.Player;
using Client.Game.InGame.UI.BlockDetail;
using Client.Game.InGame.UI.UIState;
using Client.Tests.EditModeInPlayingTest.BlockInventory;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Hostless
{
    // ブロックのサブインベントリを開くと、Webホスト無しでもtrackerがそのブロックを追い、ネットワーク情報を取得することを確かめる
    // Verifies opening a block's sub-inventory makes the tracker follow it and fetch network info, without the web host
    internal static class BlockDetailStateProbe
    {
        private const string BlockName = "TestElectricToGearGeneratorUI";

        public static async UniTask AssertAvailableAsync(IObjectResolver resolver)
        {
            var tracker = resolver.Resolve<OpenBlockDetailTracker>();
            var networkCache = resolver.Resolve<BlockNetworkInfoCache>();
            var uiStateControl = resolver.Resolve<UIStateControl>();

            // 到達範囲外で閉じないよう自機の隣に置く
            // Place it next to the player so the out-of-reach check does not close it
            var pos = Vector3Int.FloorToInt(PlayerSystemContainer.Instance.PlayerObjectController.Position) + new Vector3Int(2, 0, 0);
            PlaceBlock(BlockName, pos, BlockDirection.North);
            var blockGameObject = await WaitBlockGameObjectSpawn(pos);

            // UIStateControlに文脈付き遷移の入口は無い。先に状態だけSubInventoryへ入れ（文脈なしのエラーログが1回出る）、実プレイと同じ文脈で開き直す
            // UIStateControl has no entry for a transition with context; enter SubInventory first (one no-context error log), then reopen with the real-play context
            LogAssert.Expect(LogType.Error, "SubInventoryState: サブインベントリソースが指定されていません");
            uiStateControl.RequestTransition(UIStateEnum.SubInventory);
            for (var i = 0; i < 120 && uiStateControl.CurrentState != UIStateEnum.SubInventory; i++) await UniTask.Yield();
            Assert.AreEqual(UIStateEnum.SubInventory, uiStateControl.CurrentState);
            var subInventoryState = await BlockSubInventoryOpener.Open(blockGameObject);

            for (var i = 0; i < 120 && !ReferenceEquals(tracker.TrackedBlock, blockGameObject); i++) await UniTask.Yield();
            Assert.AreSame(blockGameObject, tracker.TrackedBlock, "サブインベントリを開いてもブロックを追跡しない");
            for (var i = 0; i < 600 && networkCache.Electric == null && networkCache.GearNetwork == null; i++) await UniTask.Yield();
            Assert.IsTrue(networkCache.Electric != null || networkCache.GearNetwork != null, "開いたブロックのネットワーク情報を取得しない");

            // 閉じたら追跡とネットワーク情報を手放す
            // Closing releases the tracking and the network info
            BlockSubInventoryOpener.Close(subInventoryState);
            uiStateControl.RequestTransition(UIStateEnum.GameScreen);
            for (var i = 0; i < 120 && tracker.TrackedBlock != null; i++) await UniTask.Yield();
            Assert.IsNull(tracker.TrackedBlock);
            Assert.IsNull(networkCache.Electric);
            Assert.IsNull(networkCache.GearNetwork);
            Assert.IsNull(networkCache.FilterSplitter);
        }
    }
}
