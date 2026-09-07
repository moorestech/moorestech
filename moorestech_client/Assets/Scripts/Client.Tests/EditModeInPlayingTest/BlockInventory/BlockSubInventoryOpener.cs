using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.BlockInventory
{
    /// <summary>
    ///     ブロックのサブインベントリを実プレイと同じ遷移コンテキストで開く
    ///     Opens a block's sub inventory with the same transit context real play uses
    /// </summary>
    public static class BlockSubInventoryOpener
    {
        public static async UniTask<SubInventoryState> Open(BlockGameObject blockGameObject)
        {
            var subInventoryState = ClientDIContext.DIContainer.DIContainerResolver.Resolve<SubInventoryState>();

            // BlockOpenInteractAction が組み立てるものと同じコンテナで入場する
            // Enter with the same container BlockOpenInteractAction builds
            var container = UITransitContextContainer.Create<ISubInventorySource>(new BlockSubInventorySource(blockGameObject));
            subInventoryState.OnEnter(new UITransitContext(UIStateEnum.SubInventory, container));

            // サーバーからのインベントリ取得完了を待つ
            // Wait until the inventory fetch from the server completes
            for (var i = 0; i < 300 && subInventoryState.CurrentSubInventory == null; i++) await UniTask.Yield();
            Assert.IsNotNull(subInventoryState.CurrentSubInventory, "sub inventory did not load");

            return subInventoryState;
        }
    }
}
